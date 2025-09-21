import { AsyncPipe, CommonModule, DatePipe } from '@angular/common';
import { Component, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { FinishedComponent } from './components/finished.component';
import { LandingComponent } from './components/landing.component';
import { LobbyComponent } from './components/lobby.component';
import { PromptSelectionComponent } from './components/prompt-selection.component';
import { ScoreboardComponent } from './components/scoreboard.component';
import { SubmissionComponent } from './components/submission.component';
import { VotingComponent } from './components/voting.component';
import { GameApiService } from './services/game-api.service';
import { GameStoreService } from './services/game-store.service';
import { GameState, PlayerIdentity, GameSettings } from './models/game-models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    AsyncPipe,
    DatePipe,
    LandingComponent,
    LobbyComponent,
    PromptSelectionComponent,
    SubmissionComponent,
    VotingComponent,
    ScoreboardComponent,
    FinishedComponent,
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnDestroy {
  readonly game$ = this.store.gameState$;
  readonly player$ = this.store.player$;
  libraryPrompts: string[] = [];
  private readonly subscriptions = new Subscription();

  constructor(private readonly store: GameStoreService, private readonly api: GameApiService) {
    this.api.listPrompts().subscribe((prompts) => (this.libraryPrompts = prompts));
    this.subscriptions.add(
      this.player$.subscribe((identity) => {
        if (identity) {
          this.store.updateConnection(true).subscribe();
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  handleStartGame(): void {
    this.store.startGame().subscribe();
  }

  handleSettingsUpdate(settings: GameSettings): void {
    this.store.updateSettings(settings).subscribe();
  }

  handleKick(playerId: string): void {
    this.store.kickPlayer(playerId).subscribe();
  }

  handlePromptConfirm(event: { prompt: string; source: 'Library' | 'Custom' }): void {
    this.store.confirmPrompt(event).subscribe();
  }

  handleSubmitBribe(event: { targetId: string; type: 'text' | 'image'; content: string }): void {
    this.store.submitBribe(event).subscribe();
  }

  handleVote(briberId: string): void {
    this.store.castVote(briberId).subscribe();
  }

  handleAdvance(): void {
    this.store.advance().subscribe();
  }

  phaseLabel(game: GameState): string {
    switch (game.phase) {
      case 'Lobby':
        return 'Lobby';
      case 'PromptSelection':
        return 'Prompt selection';
      case 'Submission':
        return 'Submission';
      case 'Voting':
        return 'Voting';
      case 'Scoreboard':
        return 'Scoreboard';
      case 'Finished':
        return 'Finished';
      default:
        return game.phase;
    }
  }

  remainingTime(game: GameState): string | null {
    if (!game.phaseEndsAt) {
      return null;
    }
    const end = new Date(game.phaseEndsAt).getTime();
    const now = Date.now();
    const remainingMs = end - now;
    if (remainingMs <= 0) {
      return '00:00';
    }
    const totalSeconds = Math.floor(remainingMs / 1000);
    const minutes = Math.floor(totalSeconds / 60).toString().padStart(2, '0');
    const seconds = (totalSeconds % 60).toString().padStart(2, '0');
    return `${minutes}:${seconds}`;
  }
}
