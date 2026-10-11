import type { PaymentStatus, RideStatus, VehicleStatus } from '@/api/types';
import type { Tone } from '@/theme';

type StatusMeta = { label: string; tone: Tone };

/** Paneldeki `VehicleStatusTagHelper` ile aynı etiket ve renkler. */
const vehicleStatus: Record<VehicleStatus, StatusMeta> = {
  Available: { label: 'Müsait', tone: 'success' },
  Reserved: { label: 'Rezerve', tone: 'warning' },
  InRide: { label: 'Sürüşte', tone: 'primary' },
  Maintenance: { label: 'Bakımda', tone: 'secondary' },
  Lost: { label: 'Kayıp', tone: 'danger' },
};

const rideStatus: Record<RideStatus, StatusMeta> = {
  Active: { label: 'Devam ediyor', tone: 'primary' },
  Completed: { label: 'Tamamlandı', tone: 'success' },
  Abandoned: { label: 'Sistem sonlandırdı', tone: 'secondary' },
};

const paymentStatus: Record<PaymentStatus, StatusMeta> = {
  NotYetCharged: { label: 'Ücretlendirilmedi', tone: 'secondary' },
  Pending: { label: 'Ödeme bekleniyor', tone: 'warning' },
  Paid: { label: 'Ödendi', tone: 'success' },
  Failed: { label: 'Ödeme alınamadı', tone: 'danger' },
};

const roles: Record<string, string> = {
  Driver: 'Sürücü',
  FleetManager: 'Filo yöneticisi',
  FieldOperator: 'Saha ekibi',
};

// Bilinmeyen bir durum gelirse panel gibi açık gri rozetle ham değeri göster.
const fallback = (value: string): StatusMeta => ({ label: value, tone: 'light' });

export const vehicleStatusMeta = (status: string): StatusMeta =>
  vehicleStatus[status as VehicleStatus] ?? fallback(status);

export const rideStatusMeta = (status: string): StatusMeta => rideStatus[status as RideStatus] ?? fallback(status);

export const paymentStatusMeta = (status: string): StatusMeta =>
  paymentStatus[status as PaymentStatus] ?? fallback(status);

export const roleLabel = (role: string): string => roles[role] ?? role;
