import { paymentStatusMeta, rideStatusMeta, roleLabel, vehicleStatusMeta } from '../labels';

describe('durum etiketleri', () => {
  it('araç durumlarını paneldeki VehicleStatusTagHelper gibi eşler', () => {
    expect(vehicleStatusMeta('Available')).toEqual({ label: 'Müsait', tone: 'success' });
    expect(vehicleStatusMeta('Reserved')).toEqual({ label: 'Rezerve', tone: 'warning' });
    expect(vehicleStatusMeta('InRide')).toEqual({ label: 'Sürüşte', tone: 'primary' });
    expect(vehicleStatusMeta('Maintenance')).toEqual({ label: 'Bakımda', tone: 'secondary' });
    expect(vehicleStatusMeta('Lost')).toEqual({ label: 'Kayıp', tone: 'danger' });
  });

  it('bilinmeyen durumu açık gri rozetle ham haliyle gösterir', () => {
    expect(vehicleStatusMeta('Charging')).toEqual({ label: 'Charging', tone: 'light' });
  });

  it('API ödeme ve sürüş durumlarını Türkçeleştirir', () => {
    expect(paymentStatusMeta('NotYetCharged').label).toBe('Ücretlendirilmedi');
    expect(paymentStatusMeta('Failed').tone).toBe('danger');
    expect(rideStatusMeta('Abandoned').label).toBe('Sistem sonlandırdı');
  });

  it('rolleri çevirir', () => {
    expect(roleLabel('Driver')).toBe('Sürücü');
    expect(roleLabel('Auditor')).toBe('Auditor');
  });
});
