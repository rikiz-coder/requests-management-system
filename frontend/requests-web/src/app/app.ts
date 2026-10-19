import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterOutlet } from '@angular/router';

import { LookupsService } from './core/api/lookups.service';
import { CurrentUserService } from './core/user/current-user.service';
import { ConnectionBannerComponent } from './shared/connection-banner.component';

/** Shell: title, the "current user" picker and the connection banner. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatToolbarModule, MatFormFieldModule, MatSelectModule, MatIconModule, ConnectionBannerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly user = inject(CurrentUserService);
  protected readonly lookups = inject(LookupsService);

  constructor() {
    // No user chosen yet: default to the first handler, so updates are attributed to someone.
    effect(() => {
      const handlers = this.lookups.lookups().handlers;
      if (!this.user.name() && handlers.length) {
        this.user.set(handlers[0]);
      }
    });
  }

  /** "Try again" from the banner: reload the current view; the URL keeps filters, page and open request. */
  protected retry(): void {
    window.location.reload();
  }
}
