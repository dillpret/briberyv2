import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BribeForTarget, GameState, PlayerIdentity } from '../models/game-models';

@Component({
  selector: 'app-voting-phase',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './voting.component.html',
  styleUrl: './voting.component.scss',
})
export class VotingComponent {
  @Input() game?: GameState | null;
  @Input() identity?: PlayerIdentity | null;
  @Output() vote = new EventEmitter<string>();

  selectedBribe?: string;
  message = '';

  get bribes(): BribeForTarget[] {
    if (!this.game || !this.identity) {
      return [];
    }
    return this.game.round?.bribesByTarget[this.identity.playerId] ?? [];
  }

  get hasVoted(): boolean {
    if (!this.game || !this.identity) {
      return false;
    }
    return (this.game.round?.pendingVotes ?? []).indexOf(this.identity.playerId) === -1;
  }

  submitVote(): void {
    if (!this.selectedBribe) {
      this.message = 'Choose your favourite bribe first.';
      return;
    }

    this.vote.emit(this.selectedBribe);
    this.message = 'Vote submitted!';
  }
}
