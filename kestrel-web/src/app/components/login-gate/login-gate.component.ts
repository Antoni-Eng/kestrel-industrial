import {
  AfterViewInit,
  Component,
  ElementRef,
  inject,
  viewChild,
} from '@angular/core';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login-gate',
  standalone: true,
  templateUrl: './login-gate.component.html',
  styleUrl: './login-gate.component.scss',
})
export class LoginGateComponent implements AfterViewInit {
  protected readonly auth = inject(AuthService);
  private readonly buttonHost =
    viewChild<ElementRef<HTMLElement>>('googleButton');

  ngAfterViewInit(): void {
    const host = this.buttonHost()?.nativeElement;
    if (host) {
      this.auth.renderButton(host);
    }
  }
}
