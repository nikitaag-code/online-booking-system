import { Pipe, PipeTransform, inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

@Pipe({ name: 'utcToLocal', standalone: true, pure: true })
export class UtcToLocalPipe implements PipeTransform {
  private readonly auth = inject(AuthService);

  transform(
    utcIso: string | null | undefined,
    format: 'time' | 'datetime' | 'date' = 'datetime',
    overrideZone?: string
  ): string {
    if (!utcIso) return '';

    const tz      = overrideZone ?? this.auth.timeZoneId;
    const date    = new Date(utcIso);
    const locale  = navigator.language;

    try {
      switch (format) {
        case 'time':
          return date.toLocaleTimeString(locale, { timeZone: tz, hour: '2-digit', minute: '2-digit' });
        case 'date':
          return date.toLocaleDateString(locale, { timeZone: tz, weekday: 'short', month: 'short', day: 'numeric' });
        case 'datetime':
        default:
          return date.toLocaleString(locale, {
            timeZone:    tz,
            weekday:     'short',
            month:       'short',
            day:         'numeric',
            hour:        '2-digit',
            minute:      '2-digit',
            timeZoneName: 'short'
          });
      }
    } catch {
      return date.toISOString();
    }
  }
}
