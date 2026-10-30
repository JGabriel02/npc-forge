import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SlowServerNotice } from './core/api/slow-server.interceptor';

/** Componente raiz: hospeda o roteador e o aviso de "servidor acordando". */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly slowServer = inject(SlowServerNotice);
}
