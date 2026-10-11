import { environment } from '../../environments/environment';

export interface KestrelConfig {
  apiBaseUrl: string;
  /** Google OAuth Web Client ID. Empty = auth UI off (matches API Development bypass). */
  googleClientId: string;
}

declare global {
  interface Window {
    __KESTREL_CONFIG__?: Partial<KestrelConfig>;
  }
}

const DEFAULT_API_BASE_URL = 'http://localhost:5258';

export function getKestrelConfig(): KestrelConfig {
  const fromWindow =
    typeof window !== 'undefined' ? window.__KESTREL_CONFIG__ : undefined;

  return {
    apiBaseUrl:
      fromWindow?.apiBaseUrl ??
      environment.apiBaseUrl ??
      DEFAULT_API_BASE_URL,
    googleClientId:
      fromWindow?.googleClientId ?? environment.googleClientId ?? '',
  };
}
