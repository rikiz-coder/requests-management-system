import { Routes } from '@angular/router';

import { RequestFullPageComponent } from './features/requests/request-full-page.component';
import { RequestsPageComponent } from './features/requests/requests-page.component';

export const routes: Routes = [
  { path: '', component: RequestsPageComponent, title: 'ניהול פניות' },
  { path: 'requests/:id', component: RequestFullPageComponent, title: 'פנייה – ניהול פניות' },
  { path: '**', redirectTo: '' },
];
