import { Injectable, computed, signal } from '@angular/core';
import { getKestrelConfig } from './config';

export interface AuthUser {
  sub: string;
  email?: string;
  name?: string;
  picture?: string;
}

interface GoogleCredentialResponse {
  credential: string;
  select_by?: string;
}

interface GoogleAccountsId {
  initialize(config: {
    client_id: string;
    callback: (response: GoogleCredentialResponse) => void;
    auto_select?: boolean;
    cancel_on_tap_outside?: boolean;
  }): void;
  renderButton(
    parent: HTMLElement,
    options: {
      theme?: string;
      size?: string;
      text?: string;
      shape?: string;
      width?: number;
    },
  ): void;
  prompt(): void;
  disableAutoSelect(): void;
}

declare global {
  interface Window {
    google?: {
      accounts: {
        id: GoogleAccountsId;
      };
    };
  }
}

const TOKEN_KEY = 'kestrel.google.idToken';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly config = getKestrelConfig();

  readonly idToken = signal<string | null>(this.readStoredToken());
  readonly user = signal<AuthUser | null>(decodeUser(this.idToken()));
  readonly ready = signal(false);

  /** Auth UI required when a Google Client ID is configured. */
  readonly authRequired = computed(() => !!this.config.googleClientId?.trim());

  readonly isSignedIn = computed(() => {
    if (!this.authRequired()) {
      return true;
    }
    return !!this.idToken() && !!this.user();
  });

  async initialize(): Promise<void> {
    const clientId = this.config.googleClientId?.trim();
    if (!clientId) {
      this.ready.set(true);
      return;
    }

    try {
      await loadGisScript();
      window.google?.accounts.id.initialize({
        client_id: clientId,
        callback: (response) => this.acceptCredential(response.credential),
        auto_select: false,
        cancel_on_tap_outside: true,
      });
    } finally {
      this.ready.set(true);
    }
  }

  renderButton(element: HTMLElement): void {
    if (!this.config.googleClientId?.trim() || !window.google) {
      return;
    }
    element.innerHTML = '';
    window.google.accounts.id.renderButton(element, {
      theme: 'outline',
      size: 'large',
      text: 'signin_with',
      shape: 'rectangular',
      width: 280,
    });
  }

  signOut(): void {
    sessionStorage.removeItem(TOKEN_KEY);
    this.idToken.set(null);
    this.user.set(null);
    window.google?.accounts.id.disableAutoSelect();
  }

  handleUnauthorized(): void {
    if (!this.authRequired()) {
      return;
    }
    this.signOut();
  }

  authorizationHeader(): Record<string, string> {
    const token = this.idToken();
    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  private acceptCredential(credential: string): void {
    sessionStorage.setItem(TOKEN_KEY, credential);
    this.idToken.set(credential);
    this.user.set(decodeUser(credential));
  }

  private readStoredToken(): string | null {
    try {
      return sessionStorage.getItem(TOKEN_KEY);
    } catch {
      return null;
    }
  }
}

function loadGisScript(): Promise<void> {
  if (window.google?.accounts?.id) {
    return Promise.resolve();
  }

  return new Promise((resolve, reject) => {
    const existing = document.querySelector<HTMLScriptElement>(
      'script[data-kestrel-gis]',
    );
    if (existing) {
      existing.addEventListener('load', () => resolve(), { once: true });
      existing.addEventListener(
        'error',
        () => reject(new Error('Failed to load Google Identity Services')),
        { once: true },
      );
      return;
    }

    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    script.dataset['kestrelGis'] = '1';
    script.onload = () => resolve();
    script.onerror = () =>
      reject(new Error('Failed to load Google Identity Services'));
    document.head.appendChild(script);
  });
}

function decodeUser(token: string | null): AuthUser | null {
  if (!token) {
    return null;
  }

  try {
    const payload = token.split('.')[1];
    if (!payload) {
      return null;
    }
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const claims = JSON.parse(json) as Record<string, unknown>;
    const sub = typeof claims['sub'] === 'string' ? claims['sub'] : null;
    if (!sub) {
      return null;
    }
    return {
      sub,
      email: typeof claims['email'] === 'string' ? claims['email'] : undefined,
      name: typeof claims['name'] === 'string' ? claims['name'] : undefined,
      picture:
        typeof claims['picture'] === 'string' ? claims['picture'] : undefined,
    };
  } catch {
    return null;
  }
}
