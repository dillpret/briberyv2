import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { GameState } from '../models/game-models';

@Component({
  selector: 'app-finished',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './finished.component.html',
  styleUrl: './finished.component.scss',
})
export class FinishedComponent {
  @Input() game?: GameState | null;

  get finalScores() {
    return [...(this.game?.players ?? [])].sort((a, b) => b.score - a.score);
  }
}
