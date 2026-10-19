import { Routes } from '@angular/router';

import { RequestsPageComponent } from './features/requests/requests-page.component';

export const routes: Routes = [
  { path: '', component: RequestsPageComponent, title: 'ניהול פניות' },
  { path: '**', redirectTo: '' },
];
