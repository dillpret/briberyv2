import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { PlayerState } from '../models/game-models';

@Component({
  selector: 'app-player-panel',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './player-panel.component.html',
  styleUrl: './player-panel.component.scss',
})
export class PlayerPanelComponent {
  @Input() players: PlayerState[] = [];
  @Input() currentPlayerId?: string;
  @Input() canKick = false;
  @Output() kickPlayer = new EventEmitter<string>();

  onKick(playerId: string): void {
    this.kickPlayer.emit(playerId);
  }
}
