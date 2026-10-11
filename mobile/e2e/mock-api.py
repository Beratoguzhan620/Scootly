#!/usr/bin/env python3
"""
Scootly API'sinin duman testleri için küçük bir taklidi (yalnızca standart kütüphane).

CI'da Android emülatörü bu sunucuya 10.0.2.2 üzerinden bağlanır; giriş, harita, rezervasyon, sürüş ve ödeme
akışı gerçek API'nin yanıt biçimleriyle (camelCase JSON, ProblemDetails hataları) denenir. Gerçek API'nin
yerini tutmaz: iş kurallarının yalnızca akış için gereken kadarı var.

    python3 e2e/mock-api.py --port 5999
    Giriş: surucu@scootly.test / Scootly2026
"""
import argparse
import base64
import json
import re
import threading
import time
import uuid
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

EMAIL = "surucu@scootly.test"
PASSWORD = "Scootly2026"
USER_ID = "7d1c2a4e-5b6f-4c3d-9e8f-0a1b2c3d4e5f"
LOCK = threading.Lock()


def iso(moment: datetime) -> str:
    return moment.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


def b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).decode().rstrip("=")


def make_token() -> str:
    header = b64url(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
    payload = b64url(json.dumps({
        "sub": USER_ID,
        "email": EMAIL,
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Driver",
        "exp": int(time.time()) + 3600,
    }).encode())
    return f"{header}.{payload}.imza"


def vehicle(vid, lat, lng, battery, brand, range_km):
    return {"id": vid, "latitude": lat, "longitude": lng, "batteryPercentage": battery,
            "status": "Available", "brand": brand, "rangeKm": range_km}


# Panelin varsayılan harita merkezi (41.0, 29.0) çevresinde
VEHICLES = [
    vehicle("3f2b8c1a-1111-4000-8000-000000000001", 41.0025, 29.0015, 86, "Segway Ninebot", 40),
    vehicle("9a7e4d2b-2222-4000-8000-000000000002", 40.9985, 29.0040, 54, "Xiaomi Pro 2", 30),
    vehicle("c41f0e93-3333-4000-8000-000000000003", 41.0045, 28.9975, 17, "Segway Ninebot", 40),
    vehicle("e8b25f70-4444-4000-8000-000000000004", 40.9970, 28.9960, 7, "Xiaomi Pro 2", 30),
]

SERVICE_AREAS = [{
    "name": "default-region",
    "boundary": [
        {"latitude": 40.9930, "longitude": 28.9900},
        {"latitude": 41.0080, "longitude": 28.9900},
        {"latitude": 41.0080, "longitude": 29.0100},
        {"latitude": 40.9930, "longitude": 29.0100},
    ],
}]

NOW = datetime.now(timezone.utc)
HISTORY = [
    {"id": str(uuid.uuid4()), "driverId": USER_ID, "vehicleId": VEHICLES[1]["id"], "status": "Completed",
     "startedAt": iso(NOW - timedelta(days=1, minutes=25)), "endedAt": iso(NOW - timedelta(days=1, minutes=7)),
     "fare": 45.0, "paymentStatus": "Paid"},
    {"id": str(uuid.uuid4()), "driverId": USER_ID, "vehicleId": VEHICLES[0]["id"], "status": "Completed",
     "startedAt": iso(NOW - timedelta(days=3, hours=2)), "endedAt": iso(NOW - timedelta(days=3, hours=1, minutes=52)),
     "fare": 20.0, "paymentStatus": "Failed"},
]

STATE = {"reserved_by_driver": None, "active_ride": None, "payment_polls": {}}


class Handler(BaseHTTPRequestHandler):
    server_version = "ScootlyMock/1.0"

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

    def authorized(self):
        if self.headers.get("Authorization", "").startswith("Bearer "):
            return True
        self.reply(401)
        return False

    @staticmethod
    def find_vehicle(vid):
        return next((v for v in VEHICLES if v["id"] == vid), None)

    @staticmethod
    def find_ride(rid):
        active = STATE["active_ride"]
        if active and active["id"] == rid:
            return active
        return next((r for r in HISTORY if r["id"] == rid), None)

    # --- uçlar -------------------------------------------------------------------------------------
    def do_GET(self):
        path = self.path.split("?")[0]
        with LOCK:
            if path in ("/health/ready", "/health/live"):
                body = b"Healthy"
                self.send_response(200)
                self.send_header("Content-Type", "text/plain")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
                return
            if path == "/api/v2/vehicles":
                items = [v for v in VEHICLES if v["status"] == "Available"]
                return self.reply(200, {"items": items, "pageNumber": 1, "pageSize": 100,
                                        "totalCount": len(items), "totalPages": 1})
            if path == "/api/v1/service-areas":
                return self.reply(200, SERVICE_AREAS)
            match = re.fullmatch(r"/api/v2/vehicles/([^/]+)", path)
            if match:
                found = self.find_vehicle(match.group(1))
                visible = found and (found["status"] == "Available" or STATE["reserved_by_driver"] == found["id"]
                                     or (STATE["active_ride"] or {}).get("vehicleId") == found["id"])
                return self.reply(200, found) if visible else self.problem(404, "Bulunamadı", "Araç bulunamadı.")

            if not self.authorized():
                return
            if path == "/api/account":
                return self.reply(200, {"id": USER_ID, "email": EMAIL, "roles": ["Driver"]})
            if path == "/api/rides/active":
                active = STATE["active_ride"]
                return self.reply(200, active) if active else self.problem(404, "Bulunamadı", "Aktif sürüş yok.")
            if path == "/api/rides":
                return self.reply(200, {"items": HISTORY, "pageNumber": 1, "pageSize": 20,
                                        "totalCount": len(HISTORY), "totalPages": 1})
            match = re.fullmatch(r"/api/rides/([^/]+)/payment-status", path)
            if match:
                ride = self.find_ride(match.group(1))
                if not ride:
                    return self.problem(404, "Bulunamadı", "Sürüş bulunamadı.")
                polls = STATE["payment_polls"].get(ride["id"], 0) + 1
                STATE["payment_polls"][ride["id"]] = polls
                if ride["paymentStatus"] == "Pending" and polls >= 3:
                    ride["paymentStatus"] = "Paid"
                return self.reply(200, {"rideId": ride["id"], "paymentStatus": ride["paymentStatus"], "fare": ride["fare"]})
            match = re.fullmatch(r"/api/rides/([^/]+)", path)
            if match:
                ride = self.find_ride(match.group(1))
                return self.reply(200, ride) if ride else self.problem(404, "Bulunamadı", "Sürüş bulunamadı.")
        self.problem(404, "Bulunamadı", "Uç bulunamadı.")

    def do_POST(self):
        path = self.path.split("?")[0]
        with LOCK:
            if path == "/api/auth/login":
                body = self.body()
                if body.get("email", "").lower() != EMAIL or body.get("password") != PASSWORD:
                    return self.reply(401)
                return self.reply(200, {"token": make_token()})
            if path == "/api/auth/register":
                return self.reply(201)

            if not self.authorized():
                return
            match = re.fullmatch(r"/api/v2/vehicles/([^/]+)/reserve", path)
            if match:
                found = self.find_vehicle(match.group(1))
                if not found:
                    return self.problem(404, "Bulunamadı", "Araç bulunamadı.")
                if STATE["reserved_by_driver"]:
                    return self.problem(409, "İşlem gerçekleştirilemedi", "Zaten aktif bir rezervasyonunuz var.")
                if found["batteryPercentage"] < 10:
                    return self.problem(409, "İş kuralı ihlali", "Aracın bataryası kiralama için çok düşük (en az %10).")
                found["status"] = "Reserved"
                STATE["reserved_by_driver"] = found["id"]
                return self.reply(200)
            if path == "/api/rides/start":
                vid = self.body().get("vehicleId")
                found = self.find_vehicle(vid)
                if not found or STATE["reserved_by_driver"] != vid:
                    return self.problem(409, "İş kuralı ihlali", "Araç rezerve edilmemiş, sürüş başlatılamaz.")
                found["status"] = "InRide"
                STATE["reserved_by_driver"] = None
                ride = {"id": str(uuid.uuid4()), "driverId": USER_ID, "vehicleId": vid, "status": "Active",
                        "startedAt": iso(datetime.now(timezone.utc) - timedelta(minutes=12, seconds=34)),
                        "endedAt": None, "fare": None, "paymentStatus": "NotYetCharged"}
                STATE["active_ride"] = ride
                return self.reply(201, {"rideId": ride["id"]})
            match = re.fullmatch(r"/api/rides/([^/]+)/complete", path)
            if match:
                ride = STATE["active_ride"]
                if not ride or ride["id"] != match.group(1):
                    return self.problem(404, "Bulunamadı", "Sürüş bulunamadı.")
                end = self.body()
                ride.update({"status": "Completed", "endedAt": iso(datetime.now(timezone.utc)),
                             "fare": 32.5, "paymentStatus": "Pending"})
                found = self.find_vehicle(ride["vehicleId"])
                found.update({"status": "Available", "latitude": end.get("endLatitude", found["latitude"]),
                              "longitude": end.get("endLongitude", found["longitude"])})
                HISTORY.insert(0, ride)
                STATE["active_ride"] = None
                return self.reply(202, {"rideId": ride["id"], "status": "Completed",
                                        "paymentStatus": "Pending", "fare": 32.5})
        self.problem(404, "Bulunamadı", "Uç bulunamadı.")

    def do_DELETE(self):
        path = self.path.split("?")[0]
        with LOCK:
            if not self.authorized():
                return
            match = re.fullmatch(r"/api/v2/vehicles/([^/]+)/reservation", path)
            if match and STATE["reserved_by_driver"] == match.group(1):
                self.find_vehicle(match.group(1))["status"] = "Available"
                STATE["reserved_by_driver"] = None
                return self.reply(204)
        self.problem(404, "Bulunamadı", "Rezervasyon bulunamadı.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5999)
    args = parser.parse_args()
    print(f"Sahte Scootly API: http://{args.host}:{args.port}", flush=True)
    ThreadingHTTPServer((args.host, args.port), Handler).serve_forever()
