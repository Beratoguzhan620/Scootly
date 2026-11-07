import { useEffect, useState } from 'react';

/** Geri sayım ve sürüş süresi için her `intervalMs`'de bir güncellenen zaman. */
export function useNow(intervalMs = 1_000, enabled = true): number {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!enabled) return;
    const interval = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(interval);
  }, [intervalMs, enabled]);

  return now;
}
