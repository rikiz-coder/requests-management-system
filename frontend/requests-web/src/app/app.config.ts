import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideNativeDateAdapter, MAT_DATE_LOCALE } from '@angular/material/core';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { connectionInterceptor, userHeaderInterceptor } from './core/http/interceptors';
import { HebrewPaginatorIntl } from './core/i18n/hebrew-paginator-intl';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([userHeaderInterceptor, connectionInterceptor])),
    provideNativeDateAdapter(),
    { provide: MAT_DATE_LOCALE, useValue: 'he-IL' },
    { provide: MatPaginatorIntl, useClass: HebrewPaginatorIntl },
  ],
};
