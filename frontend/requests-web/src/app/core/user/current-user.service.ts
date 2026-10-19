import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'requests.currentUser';

/**
 * The "current user" whose name is recorded in the status history (ChangedBy).
 * There is no authentication in this system (not required) – the user picks a name; it is kept in
 * localStorage so it survives a refresh. Documented as a known limitation.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly _name = signal<string | null>(read());

  readonly name = this._name.asReadonly();

  set(name: string): void {
    this._name.set(name);
    try {
      localStorage.setItem(STORAGE_KEY, name);
    } catch {
      // storage unavailable (private mode) – the choice still holds for this session
    }
  }
}

function read(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}
