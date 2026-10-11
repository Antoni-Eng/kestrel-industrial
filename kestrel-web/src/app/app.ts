import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterOutlet } from '@angular/router';
import { LoginGateComponent } from './components/login-gate/login-gate.component';
import { AuthService } from './core/auth.service';

@Component({
  imports: [RouterOutlet, LoginGateComponent, MatButtonModule],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly auth = inject(AuthService);
}
