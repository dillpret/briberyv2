import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { GameState, PlayerIdentity } from '../models/game-models';

@Component({
  selector: 'app-scoreboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './scoreboard.component.html',
  styleUrl: './scoreboard.component.scss',
})
export class ScoreboardComponent {
  @Input() game?: GameState | null;
  @Input() identity?: PlayerIdentity | null;
  @Output() advance = new EventEmitter<void>();

  get latestSummary() {
    return this.game?.completedRounds[this.game.completedRounds.length - 1];
  }

  playerName(playerId: string): string {
    return this.game?.players.find((p) => p.id === playerId)?.name || 'Player';
  }

  onAdvance(): void {
    if (this.identity?.isHost) {
      this.advance.emit();
    }
  }
}
