import { ChangeDetectionStrategy, Component, inject, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

import { ConnectionService } from '../core/http/connection.service';

/** One banner when the API cannot be reached, instead of an error in every part of the screen. */
@Component({
  selector: 'app-connection-banner',
  imports: [MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (connection.offline()) {
      <div class="banner" role="alert">
        <mat-icon>cloud_off</mat-icon>
        <span>אין חיבור לשרת. ייתכן שהשרת לא פועל או עדיין עולה.</span>
        <button mat-stroked-button (click)="retry.emit()">ניסיון חוזר</button>
      </div>
    }
  `,
  styles: `
    .banner { display: flex; align-items: center; gap: 12px; padding: 10px 16px; background: #fdecea; color: #8c1d18; }
  `,
})
export class ConnectionBannerComponent {
  protected readonly connection = inject(ConnectionService);
  readonly retry = output<void>();
}
