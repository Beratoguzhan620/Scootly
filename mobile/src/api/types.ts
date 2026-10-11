/**
 * Scootly API sözleşmeleri — `src/Scootly.Api/Contracts` kayıtlarının TypeScript karşılıkları.
 * ASP.NET Core JSON'u camelCase yazdığı için alan adları küçük harfle başlar.
 */

export type Guid = string;

/** `Scootly.Domain.Fleet.VehicleStatus` */
export type VehicleStatus = 'Available' | 'Reserved' | 'InRide' | 'Maintenance' | 'Lost';

/** `Scootly.Domain.Riding.RideStatus` */
export type RideStatus = 'Active' | 'Completed' | 'Abandoned';

/** `RidesController.ToPaymentStatusText` — `None` API'de `NotYetCharged` olarak döner. */
export type PaymentStatus = 'NotYetCharged' | 'Pending' | 'Paid' | 'Failed';

export type Role = 'Driver' | 'FleetManager' | 'FieldOperator';

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface TokenResponse {
  token: string;
}

/** `VehicleResponseV2` (`/api/v2/vehicles`) */
export interface Vehicle {
  id: Guid;
  latitude: number;
  longitude: number;
  batteryPercentage: number;
  status: VehicleStatus;
  brand: string;
  rangeKm: number;
}

export interface Ride {
  id: Guid;
  driverId: Guid;
  vehicleId: Guid;
  status: RideStatus;
  /** UTC */
  startedAt: string;
  /** UTC */
  endedAt: string | null;
  fare: number | null;
  paymentStatus: PaymentStatus;
}

export interface StartRideResponse {
  rideId: Guid;
}

export interface CompleteRideResponse {
  rideId: Guid;
  status: RideStatus;
  paymentStatus: PaymentStatus;
  fare: number;
}

export interface PaymentStatusResponse {
  rideId: Guid;
  paymentStatus: PaymentStatus;
  fare: number | null;
}

export interface BoundaryPoint {
  latitude: number;
  longitude: number;
}

export interface ServiceArea {
  name: string;
  boundary: BoundaryPoint[];
}

export interface Account {
  id: Guid;
  email: string;
  roles: string[];
}

/** RFC 7807 — API tüm hataları bu biçimde döner (`ResultExtensions`, `GlobalExceptionHandler`). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
