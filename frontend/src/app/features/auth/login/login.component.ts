import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, InputTextModule, PasswordModule, ButtonModule, MessageModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
private auth = inject(AuthService);
  private router = inject(Router);

  username = '';
  password = '';
  loading = signal(false);
  errorMsg = signal('');

  onLogin(): void {
    if (!this.username || !this.password) {
      this.errorMsg.set('Please enter username and password.');
      return;
    }
    this.loading.set(true);
    this.errorMsg.set('');

    this.auth.login({ username: this.username, password: this.password }).subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success) this.router.navigate(['/dashboard']);
        else this.errorMsg.set(res.message ?? 'Login failed.');
      },
      error: () => {
        this.loading.set(false);
        this.errorMsg.set('Invalid username or password.');
      }
    });
  }
}
