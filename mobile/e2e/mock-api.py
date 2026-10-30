#!/usr/bin/env python3
"""
Scootly API'sinin duman testleri ve web önizlemesi için küçük bir taklidi (yalnızca standart kütüphane).

CI'da Android emülatörü bu sunucuya 10.0.2.2 üzerinden bağlanır; giriş, harita, rezervasyon, QR ile başlatma,
sürüş, ödeme ve filo yönetimi akışı gerçek API'nin yanıt biçimleriyle (camelCase JSON, ProblemDetails hataları)
denenir. Gerçek API'nin yerini tutmaz: iş kurallarının yalnızca akış için gereken kadarı var.

    python3 e2e/mock-api.py --port 5999
    python3 e2e/mock-api.py --center 47.3725,8.5405 --demo     # daha çok araç ve geçmiş
    python3 e2e/mock-api.py --legacy                           # rezervasyon ucu olmayan eski sunucu

Hesaplar (parola hepsinde Scootly2026):
    surucu@scootly.test   Driver
    yonetici@scootly.test FleetManager (ilk filo yöneticisi gibi yalnızca bu rol)
    saha@scootly.test     FieldOperator
"""
import argparse
import base64
import json
import math
import random
import re
import threading
import time
import uuid
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

PASSWORD = "Scootly2026"
USERS = {
    "surucu@scootly.test": {"id": "7d1c2a4e-5b6f-4c3d-9e8f-0a1b2c3d4e5f", "roles": ["Driver"]},
    "yonetici@scootly.test": {"id": "1a2b3c4d-0000-4000-8000-00000000f1ee", "roles": ["FleetManager"]},
    "saha@scootly.test": {"id": "5e6f7a8b-0000-4000-8000-0000000005a4", "roles": ["FieldOperator"]},
}
ROLE_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
RESERVATION_MINUTES = 10
LOCK = threading.Lock()
OPTIONS = {"legacy": False}


def iso(moment: datetime) -> str:
    return moment.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


def now() -> datetime:
    return datetime.now(timezone.utc)


def b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).decode().rstrip("=")


def make_token(email: str) -> str:
    user = USERS[email]
    header = b64url(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
    payload = b64url(json.dumps({
        "sub": user["id"],
        "email": email,
        ROLE_CLAIM: user["roles"][0] if len(user["roles"]) == 1 else user["roles"],
        "exp": int(time.time()) + 3600,
    }).encode())
    return f"{header}.{payload}.imza"


def read_token(token: str):
    try:
        payload = token.split(".")[1]
        payload += "=" * (-len(payload) % 4)
        claims = json.loads(base64.urlsafe_b64decode(payload))
        return next((email for email, user in USERS.items() if user["id"] == claims.get("sub")), None)
    except Exception:  # noqa: BLE001
        return None


def vehicle(vid, lat, lng, battery, brand, range_km, status="Available"):
    return {"id": vid, "latitude": lat, "longitude": lng, "batteryPercentage": battery,
            "status": status, "brand": brand, "rangeKm": range_km}


VEHICLES = []
SERVICE_AREAS = []
HISTORY = {}
STATE = {
    "reservations": {},   # sürücü kimliği -> {"vehicleId", "reservedAt"}
    "rides": {},          # sürücü kimliği -> aktif sürüş
    "payment_polls": {},
}


def seed(center, demo):
    lat0, lng0 = center
    # İlk dördü testlerin beklediği araçlar (emülatörün konumuna en yakın kiralanabilir olan %86).
    VEHICLES.extend([
        vehicle("3f2b8c1a-1111-4000-8000-000000000001", lat0 + 0.0025, lng0 + 0.0015, 86, "Segway Ninebot", 40),
        vehicle("9a7e4d2b-2222-4000-8000-000000000002", lat0 - 0.0015, lng0 + 0.0040, 54, "Xiaomi Pro 2", 30),
        vehicle("c41f0e93-3333-4000-8000-000000000003", lat0 + 0.0045, lng0 - 0.0025, 17, "Segway Ninebot", 40),
        vehicle("e8b25f70-4444-4000-8000-000000000004", lat0 - 0.0030, lng0 - 0.0040, 7, "Xiaomi Pro 2", 30),
        # Filo ekibinin gördüğü, sürücüye görünmeyen araçlar
        vehicle("5b1d7e20-5555-4000-8000-000000000005", lat0 + 0.0010, lng0 - 0.0045, 63, "Segway Ninebot", 40, "Maintenance"),
        vehicle("d7c3a9f1-6666-4000-8000-000000000006", lat0 - 0.0042, lng0 + 0.0012, 38, "Xiaomi Pro 2", 30, "Lost"),
    ])
    if demo:
        rng = random.Random(11)
        for i in range(26):
            angle = rng.random() * math.tau
            distance = 0.003 + rng.random() * 0.010
            status = "Maintenance" if i in (4, 17) else "Lost" if i == 9 else "Available"
            VEHICLES.append(vehicle(f"{i:08x}-5555-4000-8000-{i:012d}", lat0 + math.sin(angle) * distance * 0.7,
                                    lng0 + math.cos(angle) * distance,
                                    rng.choice([92, 81, 77, 66, 58, 49, 41, 33, 28, 21, 64, 88]),
                                    rng.choice(["Segway Ninebot", "Xiaomi Pro 2"]), 35, status))
    SERVICE_AREAS.append({
        "name": "Merkez",
        "boundary": [
            {"latitude": lat0 + 0.010, "longitude": lng0 - 0.016},
            {"latitude": lat0 + 0.012, "longitude": lng0 + 0.013},
            {"latitude": lat0 - 0.007, "longitude": lng0 + 0.018},
            {"latitude": lat0 - 0.011, "longitude": lng0 - 0.011},
        ],
    })
    driver = USERS["surucu@scootly.test"]["id"]
    start = now()

    def ride(vid, started, minutes, fare, payment="Paid"):
        return {"id": str(uuid.uuid4()), "driverId": driver, "vehicleId": vid, "status": "Completed",
                "startedAt": iso(started), "endedAt": iso(started + timedelta(minutes=minutes)),
                "fare": fare, "paymentStatus": payment}

    HISTORY[driver] = [
        ride(VEHICLES[1]["id"], start - timedelta(days=1, minutes=25), 18, 45.0),
        ride(VEHICLES[0]["id"], start - timedelta(days=3, hours=2), 8, 20.0, "Failed"),
    ]
    if demo:
        HISTORY[driver].extend([
            ride(VEHICLES[8]["id"], start - timedelta(days=4, hours=5), 12, 30.0),
            ride(VEHICLES[10]["id"], start - timedelta(days=31), 26, 65.0),
        ])


class Handler(BaseHTTPRequestHandler):
    server_version = "ScootlyMock/2.0"

    def log_message(self, fmt, *args):
        print(f"{self.command} {self.path} -> {fmt % args}", flush=True)

    # --- yardımcılar -------------------------------------------------------------------------------
    def end_headers(self):
        # Web önizlemesi (npm run web) başka bir porttan bağlanabilsin diye.
        self.send_header("Access-Control-Allow-Origin", "*")
        super().end_headers()

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Headers", "Authorization, Content-Type, Accept")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, DELETE")
        self.end_headers()

    def reply(self, status, body=None, problem=False):
        data = b"" if body is None else json.dumps(body).encode()
        self.send_response(status)
        if body is not None:
            self.send_header("Content-Type", "application/problem+json" if problem else "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def problem(self, status, title, detail):
        self.reply(status, {"title": title, "status": status, "detail": detail}, problem=True)

    def body(self):
        length = int(self.headers.get("Content-Length") or 0)
        return json.loads(self.rfile.read(length) or b"{}") if length else {}

    def user(self):
        """Token'daki kullanıcı (e-posta, kimlik, roller); yoksa None."""
        header = self.headers.get("Authorization", "")
        if not header.startswith("Bearer "):
            return None
        email = read_token(header[7:])
        return (email, USERS[email]["id"], USERS[email]["roles"]) if email else None

    def require(self, *roles):
        current = self.user()
        if not current:
            self.reply(401)
            return None
        if roles and not set(roles) & set(current[2]):
            self.reply(403)
            return None
        return current

    @staticmethod
    def find_vehicle(vid):
        return next((v for v in VEHICLES if v["id"] == vid), None)

    @staticmethod
    def find_ride(driver, rid):
        active = STATE["rides"].get(driver)
        if active and active["id"] == rid:
            return active
        return next((r for r in HISTORY.get(driver, []) if r["id"] == rid), None)

    @staticmethod
    def expire_reservations():
        cutoff = now() - timedelta(minutes=RESERVATION_MINUTES)
        for driver, reservation in list(STATE["reservations"].items()):
            if reservation["reservedAt"] <= cutoff:
                found = Handler.find_vehicle(reservation["vehicleId"])
                if found and found["status"] == "Reserved":
                    found["status"] = "Available"
                del STATE["reservations"][driver]

    @staticmethod
    def reserved_by(vid):
        return next((d for d, r in STATE["reservations"].items() if r["vehicleId"] == vid), None)

    def is_fleet(self):
        current = self.user()
        return bool(current and {"FleetManager", "FieldOperator"} & set(current[2]))

    # --- uçlar -------------------------------------------------------------------------------------
    def do_GET(self):
        url = urlparse(self.path)
        path = url.path
        query = {key: values[0] for key, values in parse_qs(url.query).items()}
        with LOCK:
            self.expire_reservations()
            if path in ("/health/ready", "/health/live"):
                body = b"Healthy"
                self.send_response(200)
                self.send_header("Content-Type", "text/plain")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
                return
            if path == "/api/v2/vehicles":
                only_available = query.get("onlyAvailable", "false").lower() == "true" or not self.is_fleet()
                items = [v for v in VEHICLES if not only_available or v["status"] == "Available"]
                if "minLatitude" in query:
                    items = [v for v in items
                             if float(query["minLatitude"]) <= v["latitude"] <= float(query["maxLatitude"])
                             and float(query["minLongitude"]) <= v["longitude"] <= float(query["maxLongitude"])]
                page, size = int(query.get("pageNumber", 1)), int(query.get("pageSize", 20))
                return self.reply(200, {"items": items[(page - 1) * size: page * size], "pageNumber": page,
                                        "pageSize": size, "totalCount": len(items),
                                        "totalPages": max(1, math.ceil(len(items) / size))})
            if path == "/api/v1/service-areas":
                return self.reply(200, SERVICE_AREAS)
            match = re.fullmatch(r"/api/v[12]/vehicles/([^/]+)", path)
            if match:
                found = self.find_vehicle(match.group(1))
                current = self.user()
                driver = current[1] if current else None
                visible = found and (found["status"] == "Available" or self.is_fleet()
                                     or (driver and self.reserved_by(found["id"]) == driver)
                                     or (driver and (STATE["rides"].get(driver) or {}).get("vehicleId") == found["id"]))
                return self.reply(200, found) if visible else self.problem(404, "Bulunamadı", "Araç bulunamadı.")

            current = self.require()
            if not current:
                return
            email, user_id, roles = current
            if path == "/api/account":
                return self.reply(200, {"id": user_id, "email": email, "roles": roles})

            if path == "/api/reservations/active" and not OPTIONS["legacy"]:
                if "Driver" not in roles:
                    return self.reply(403)
                reservation = STATE["reservations"].get(user_id)
                if not reservation:
                    return self.reply(204)
                reserved_at = reservation["reservedAt"]
                return self.reply(200, {
                    "vehicleId": reservation["vehicleId"],
                    "reservedAt": iso(reserved_at),
                    "expiresAt": iso(reserved_at + timedelta(minutes=RESERVATION_MINUTES)),
                    "vehicle": self.find_vehicle(reservation["vehicleId"]),
                })

            if path.startswith("/api/rides"):
                if "Driver" not in roles:
                    return self.reply(403)
                if path == "/api/rides/active":
                    active = STATE["rides"].get(user_id)
                    return self.reply(200, active) if active else self.problem(404, "Bulunamadı", "Aktif sürüş yok.")
                if path == "/api/rides":
                    history = HISTORY.get(user_id, [])
                    return self.reply(200, {"items": history, "pageNumber": 1, "pageSize": 20,
                                            "totalCount": len(history), "totalPages": 1})
                match = re.fullmatch(r"/api/rides/([^/]+)/payment-status", path)
                if match:
                    ride = self.find_ride(user_id, match.group(1))
                    if not ride:
                        return self.problem(404, "Bulunamadı", "Sürüş bulunamadı.")
                    polls = STATE["payment_polls"].get(ride["id"], 0) + 1
                    STATE["payment_polls"][ride["id"]] = polls
                    if ride["paymentStatus"] == "Pending" and polls >= 3:
                        ride["paymentStatus"] = "Paid"
                    return self.reply(200, {"rideId": ride["id"], "paymentStatus": ride["paymentStatus"],
                                            "fare": ride["fare"]})
                match = re.fullmatch(r"/api/rides/([^/]+)", path)
                if match:
                    ride = self.find_ride(user_id, match.group(1))
                    return self.reply(200, ride) if ride else self.problem(404, "Bulunamadı", "Sürüş bulunamadı.")
        self.problem(404, "Bulunamadı", "Uç bulunamadı.")

    def do_POST(self):
        path = urlparse(self.path).path
        with LOCK:
            self.expire_reservations()
            if path == "/api/auth/login":
                body = self.body()
                email = body.get("email", "").lower()
                if email not in USERS or body.get("password") != PASSWORD:
                    return self.reply(401)
                return self.reply(200, {"token": make_token(email)})
            if path == "/api/auth/register":
                return self.reply(201)

            if re.fullmatch(r"/api/v[12]/vehicles", path):
                if not self.require("FleetManager"):
                    return
                body = self.body()
                if not str(body.get("brand", "")).strip():
                    return self.problem(400, "Geçersiz istek", "Marka 1-100 karakter olmalı.")
                created = vehicle(str(uuid.uuid4()), float(body["latitude"]), float(body["longitude"]),
                                  int(body["batteryPercentage"]), body["brand"].strip(), int(body["rangeKm"]))
                VEHICLES.append(created)
                return self.reply(201, {"id": created["id"]})

            match = re.fullmatch(r"/api/v[12]/vehicles/([^/]+)/(maintenance|lost|return-to-service)", path)
            if match:
                if not self.require("FleetManager", "FieldOperator"):
                    return
                found = self.find_vehicle(match.group(1))
                if not found:
                    return self.problem(404, "Bulunamadı", "Araç bulunamadı.")
                action, status = match.group(2), found["status"]
                if action == "return-to-service":
                    if status not in ("Maintenance", "Lost"):
                        return self.problem(409, "İş kuralı ihlali",
                                            "Yalnızca bakımdaki veya kayıp bir araç hizmete döndürülebilir.")
                    found["status"] = "Available"
                    return self.reply(204)
                if status == "InRide":
                    return self.problem(409, "İş kuralı ihlali", "Sürüşteki bir araç bakıma alınamaz; önce sürüş sonlanmalı.")
                target = "Maintenance" if action == "maintenance" else "Lost"
                if status == target:
                    return self.problem(409, "İş kuralı ihlali",
                                        "Araç zaten bakımda." if target == "Maintenance" else "Araç zaten kayıp olarak işaretli.")
                driver = self.reserved_by(found["id"])
                if driver:
                    del STATE["reservations"][driver]
                found["status"] = target
                return self.reply(204)

            current = self.require("Driver")
            if not current:
                return
            user_id = current[1]
            match = re.fullmatch(r"/api/v[12]/vehicles/([^/]+)/reserve", path)
            if match:
                found = self.find_vehicle(match.group(1))
                if not found:
                    return self.problem(404, "Bulunamadı", "Araç bulunamadı.")
                if user_id in STATE["reservations"]:
                    return self.problem(409, "İşlem gerçekleştirilemedi", "Zaten aktif bir rezervasyonunuz var.")
                if user_id in STATE["rides"]:
                    return self.problem(409, "İşlem gerçekleştirilemedi",
                                        "Devam eden bir sürüşünüz varken yeni rezervasyon yapamazsınız.")
                if found["status"] != "Available":
                    return self.problem(409, "İş kuralı ihlali", "Araç müsait değil, rezerve edilemez.")
                if found["batteryPercentage"] < 10:
                    return self.problem(409, "İş kuralı ihlali", "Aracın bataryası kiralama için çok düşük (en az %10).")
                found["status"] = "Reserved"
                STATE["reservations"][user_id] = {"vehicleId": found["id"], "reservedAt": now()}
                return self.reply(200)
            if path == "/api/rides/start":
                vid = self.body().get("vehicleId")
                found = self.find_vehicle(vid)
                reservation = STATE["reservations"].get(user_id)
                if not found or not reservation or reservation["vehicleId"] != vid:
                    return self.problem(409, "İş kuralı ihlali", "Araç rezerve edilmemiş, sürüş başlatılamaz.")
                found["status"] = "InRide"
                del STATE["reservations"][user_id]
                ride = {"id": str(uuid.uuid4()), "driverId": user_id, "vehicleId": vid, "status": "Active",
                        "startedAt": iso(now() - timedelta(minutes=12, seconds=34)),
                        "endedAt": None, "fare": None, "paymentStatus": "NotYetCharged"}
                STATE["rides"][user_id] = ride
                return self.reply(201, {"rideId": ride["id"]})
            match = re.fullmatch(r"/api/rides/([^/]+)/complete", path)
            if match:
                ride = STATE["rides"].get(user_id)
                if not ride or ride["id"] != match.group(1):
                    return self.problem(404, "Bulunamadı", "Sürüş bulunamadı.")
                end = self.body()
                ride.update({"status": "Completed", "endedAt": iso(now()), "fare": 32.5, "paymentStatus": "Pending"})
                found = self.find_vehicle(ride["vehicleId"])
                found.update({"status": "Available", "latitude": end.get("endLatitude", found["latitude"]),
                              "longitude": end.get("endLongitude", found["longitude"])})
                HISTORY.setdefault(user_id, []).insert(0, ride)
                del STATE["rides"][user_id]
                return self.reply(202, {"rideId": ride["id"], "status": "Completed",
                                        "paymentStatus": "Pending", "fare": 32.5})
        self.problem(404, "Bulunamadı", "Uç bulunamadı.")

    def do_DELETE(self):
        path = urlparse(self.path).path
        with LOCK:
            self.expire_reservations()
            current = self.require("Driver")
            if not current:
                return
            match = re.fullmatch(r"/api/v[12]/vehicles/([^/]+)/reservation", path)
            reservation = STATE["reservations"].get(current[1])
            if match and reservation and reservation["vehicleId"] == match.group(1):
                self.find_vehicle(match.group(1))["status"] = "Available"
                del STATE["reservations"][current[1]]
                return self.reply(204)
        self.problem(404, "Bulunamadı", "Rezervasyon bulunamadı.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5999)
    parser.add_argument("--center", default="41.0,29.0", help="araçların çevresinde durduğu nokta: enlem,boylam")
    parser.add_argument("--demo", action="store_true", help="ekran görüntüleri için daha çok araç ve geçmiş")
    parser.add_argument("--legacy", action="store_true", help="GET /api/reservations/active yokmuş gibi davran")
    args = parser.parse_args()
    OPTIONS["legacy"] = args.legacy
    seed(tuple(float(part) for part in args.center.split(",")), args.demo)
    print(f"Sahte Scootly API: http://{args.host}:{args.port}", flush=True)
    ThreadingHTTPServer((args.host, args.port), Handler).serve_forever()
