import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GameState, PlayerIdentity, PromptSelectionState } from '../models/game-models';

@Component({
  selector: 'app-prompt-selection',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './prompt-selection.component.html',
  styleUrl: './prompt-selection.component.scss',
})
export class PromptSelectionComponent {
  @Input() game?: GameState | null;
  @Input() identity?: PlayerIdentity | null;
  @Input() libraryPrompts: string[] = [];
  @Output() confirm = new EventEmitter<{ prompt: string; source: 'Library' | 'Custom' }>();

  mode: 'library' | 'custom' = 'library';
  selectedLibraryPrompt?: string;
  customPrompt = '';
  message = '';

  get roundPrompts(): Record<string, PromptSelectionState> {
    return this.game?.round?.promptsByTarget ?? {};
  }

  get pendingPlayers(): string[] {
    return this.game?.round?.pendingPromptConfirmations ?? [];
  }

  get hasSubmitted(): boolean {
    if (!this.identity) {
      return false;
    }
    return !!this.roundPrompts[this.identity.playerId];
  }

  chooseMode(mode: 'library' | 'custom'): void {
    this.mode = mode;
    this.message = '';
  }

  submitPrompt(): void {
    if (!this.identity) {
      return;
    }

    let prompt = '';
    let source: 'Library' | 'Custom';

    if (this.mode === 'library') {
      prompt = (this.selectedLibraryPrompt ?? '').trim();
      source = 'Library';
      if (!prompt) {
        this.message = 'Select a prompt from the list first.';
        return;
      }
    } else {
      prompt = this.customPrompt.trim();
      source = 'Custom';
      if (!prompt) {
        this.message = 'Enter a custom prompt before confirming.';
        return;
      }
    }

    this.confirm.emit({ prompt, source });
    this.message = 'Prompt submitted!';
  }
}
