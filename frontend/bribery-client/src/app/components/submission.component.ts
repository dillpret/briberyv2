import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BribeRecordEntry, GameState, PlayerIdentity } from '../models/game-models';

interface DraftBribe {
  mode: 'text' | 'image';
  text: string;
  image: string;
}

@Component({
  selector: 'app-submission-phase',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './submission.component.html',
  styleUrl: './submission.component.scss',
})
export class SubmissionComponent implements OnChanges {
  @Input() game?: GameState | null;
  @Input() identity?: PlayerIdentity | null;
  @Output() submit = new EventEmitter<{ targetId: string; type: 'text' | 'image'; content: string }>();

  drafts = new Map<string, DraftBribe>();

  ngOnChanges(changes: SimpleChanges): void {
    if (this.game && this.identity) {
      const targets = this.game.round?.assignments[this.identity.playerId] ?? [];
      targets.forEach((targetId) => {
        this.ensureDraft(targetId);
      });
      // remove drafts for old targets
      for (const key of Array.from(this.drafts.keys())) {
        if (!targets.includes(key)) {
          this.drafts.delete(key);
        }
      }
    }
  }

  get assignments(): string[] {
    if (!this.game || !this.identity) {
      return [];
    }
    return this.game.round?.assignments[this.identity.playerId] ?? [];
  }

  get submissions(): Record<string, BribeRecordEntry[]> {
    return this.game?.round?.submissions ?? {};
  }

  get promptsByTarget() {
    return this.game?.round?.promptsByTarget ?? {};
  }

  isSubmitted(targetId: string): boolean {
    if (!this.identity) {
      return false;
    }
    const submitted = this.submissions[this.identity.playerId] ?? [];
    return submitted.some((entry) => entry.targetId === targetId);
  }

  submitBribe(targetId: string): void {
    const draft = this.ensureDraft(targetId);

    let content = '';
    if (draft.mode === 'text') {
      content = draft.text.trim();
      if (!content) {
        return;
      }
      draft.text = '';
    } else {
      content = draft.image.trim();
      if (!content) {
        return;
      }
      draft.image = '';
    }

    this.submit.emit({ targetId, type: draft.mode, content });
  }

  switchMode(targetId: string, mode: 'text' | 'image'): void {
    const draft = this.ensureDraft(targetId);
    draft.mode = mode;
  }

  draftFor(targetId: string): DraftBribe {
    return this.ensureDraft(targetId);
  }

  isTextMode(targetId: string): boolean {
    return this.ensureDraft(targetId).mode === 'text';
  }

  isImageMode(targetId: string): boolean {
    return this.ensureDraft(targetId).mode === 'image';
  }

  promptText(targetId: string): string {
    const prompt = this.promptsByTarget[targetId];
    return prompt?.text || 'No prompt yet';
  }

  playerName(targetId: string): string {
    return this.game?.players.find((p) => p.id === targetId)?.name || 'Player';
  }

  private ensureDraft(targetId: string): DraftBribe {
    let draft = this.drafts.get(targetId);
    if (!draft) {
      draft = { mode: 'text', text: '', image: '' };
      this.drafts.set(targetId, draft);
    }
    return draft;
  }
}
