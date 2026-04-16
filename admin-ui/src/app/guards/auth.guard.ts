import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AuthStateService } from '../services/auth-state.service';

export const authGuard: CanActivateFn = async (_route, _state) => {
  const authState = inject(AuthStateService);
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authState.isAuthenticated()) return true;

  // Wait up to 2s for postMessage from parent (iframe auto-login)
  const apiKey = await waitForPostMessageApiKey(2000);
  if (apiKey) {
    try {
      await authService.exchangeApiKey(apiKey);
      return true;
    } catch {
      // key invalid / revoked → fall through to login
    }
  }

  router.navigate(['/login']);
  return false;
};

function waitForPostMessageApiKey(timeoutMs: number): Promise<string | null> {
  return new Promise(resolve => {
    const timer = setTimeout(() => {
      window.removeEventListener('message', handler);
      resolve(null);
    }, timeoutMs);

    const handler = (event: MessageEvent) => {
      if (event.data?.type === 'auth' && typeof event.data.apiKey === 'string') {
        clearTimeout(timer);
        window.removeEventListener('message', handler);
        // Notify the parent that we are ready (optional UX improvement)
        resolve(event.data.apiKey as string);
      }
    };

    // Tell parent we're ready
    if (window.parent !== window) {
      window.parent.postMessage({ type: 'ready' }, '*');
    }

    window.addEventListener('message', handler);
  });
}
