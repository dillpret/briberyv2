import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { GameStoreService } from '../services/game-store.service';
import { GameSettings } from '../models/game-models';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss',
})
export class LandingComponent {
  showHowToPlay = false;
  creationError = '';
  joinError = '';

  readonly createForm = this.fb.group({
    hostName: ['', [Validators.required, Validators.maxLength(20)]],
    totalRounds: [3, [Validators.required, Validators.min(1), Validators.max(100)]],
    promptSelectionTimerSeconds: [45, [Validators.required, Validators.min(0), Validators.max(600)]],
    submissionTimerSeconds: [75, [Validators.required, Validators.min(0), Validators.max(600)]],
    votingTimerSeconds: [60, [Validators.required, Validators.min(0), Validators.max(600)]],
    resultsTimerSeconds: [30, [Validators.required, Validators.min(0), Validators.max(600)]],
    customPromptsEnabled: [false],
  });

  readonly joinForm = this.fb.group({
    code: ['', [Validators.required, Validators.minLength(4), Validators.maxLength(4)]],
    name: ['', [Validators.required, Validators.maxLength(20)]],
  });

  constructor(private readonly fb: FormBuilder, private readonly store: GameStoreService) {}

  createGame(): void {
    this.creationError = '';
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    const settings = this.getSettingsFromForm();
    const hostName = this.createForm.value.hostName!.trim();
    this.store.createGame(hostName, settings).subscribe({
      error: (err) => {
        this.creationError = err?.error?.error ?? 'Could not create game. Please try again.';
      },
    });
  }

  joinGame(): void {
    this.joinError = '';
    if (this.joinForm.invalid) {
      this.joinForm.markAllAsTouched();
      return;
    }

    const code = this.joinForm.value.code!.trim().toUpperCase();
    const name = this.joinForm.value.name!.trim();
    this.store.joinGame(code, name).subscribe({
      error: (err) => {
        this.joinError = err?.error?.error ?? 'Unable to join game. Check the code and try again.';
      },
    });
  }

  toggleHowToPlay(): void {
    this.showHowToPlay = !this.showHowToPlay;
  }

  private getSettingsFromForm(): GameSettings {
    return {
      totalRounds: this.createForm.value.totalRounds!,
      promptSelectionTimerSeconds: this.createForm.value.promptSelectionTimerSeconds!,
      submissionTimerSeconds: this.createForm.value.submissionTimerSeconds!,
      votingTimerSeconds: this.createForm.value.votingTimerSeconds!,
      resultsTimerSeconds: this.createForm.value.resultsTimerSeconds!,
      customPromptsEnabled: this.createForm.value.customPromptsEnabled!,
    };
  }
}
