import { Component, inject, signal } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';
import { ButtonModule } from 'primeng/button';
import { AvatarModule } from 'primeng/avatar';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CommonModule, ButtonModule, AvatarModule, ToastModule],
  providers: [MessageService],
  templateUrl: './layout.component.html', 
  styleUrl: './layout.component.css'
})
export class LayoutComponent {
  auth = inject(AuthService);
  sidebarOpen = signal(true);
}
