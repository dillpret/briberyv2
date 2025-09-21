import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { GameState, GameSettings, PlayerIdentity } from '../models/game-models';
import { PlayerPanelComponent } from './player-panel.component';

@Component({
  selector: 'app-lobby',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PlayerPanelComponent],
  templateUrl: './lobby.component.html',
  styleUrl: './lobby.component.scss',
})
export class LobbyComponent implements OnChanges {
  @Input() game?: GameState | null;
  @Input() identity?: PlayerIdentity | null;
  @Output() startGame = new EventEmitter<void>();
  @Output() updateSettings = new EventEmitter<GameSettings>();
  @Output() kickPlayer = new EventEmitter<string>();

  copied = false;
  readonly location = window.location;

  readonly settingsForm = this.fb.group({
    totalRounds: [3, [Validators.required, Validators.min(1), Validators.max(100)]],
    promptSelectionTimerSeconds: [45, [Validators.required, Validators.min(0), Validators.max(600)]],
    submissionTimerSeconds: [75, [Validators.required, Validators.min(0), Validators.max(600)]],
    votingTimerSeconds: [60, [Validators.required, Validators.min(0), Validators.max(600)]],
    resultsTimerSeconds: [30, [Validators.required, Validators.min(0), Validators.max(600)]],
    customPromptsEnabled: [false],
  });

  constructor(private readonly fb: FormBuilder) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (this.game) {
      this.settingsForm.patchValue(this.game.settings);
    }
  }

  canStart(): boolean {
    return !!this.identity?.isHost && (this.game?.players.filter((p) => !p.isWaiting).length ?? 0) >= 3;
  }

  onStart(): void {
    if (this.canStart()) {
      this.startGame.emit();
    }
  }

  onSaveSettings(): void {
    if (!this.identity?.isHost) {
      return;
    }

    if (this.settingsForm.invalid) {
      this.settingsForm.markAllAsTouched();
      return;
    }

    this.updateSettings.emit(this.settingsForm.value as GameSettings);
  }

  copyCode(): void {
    if (!this.game) {
      return;
    }
    navigator.clipboard.writeText(this.game.code).then(() => {
      this.copied = true;
      setTimeout(() => (this.copied = false), 2000);
    });
  }
}
