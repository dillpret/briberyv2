import { Injectable, OnDestroy } from '@angular/core';
import { BehaviorSubject, filter, map, Observable, Subscription, switchMap, tap, timer } from 'rxjs';
import { GameState, GameSettings, PlayerIdentity, PlayerState } from '../models/game-models';
import {
  AdvanceRequest,
  ConnectionUpdateRequest,
  JoinGameResponse,
  KickPlayerRequest,
  PromptSelectionRequest,
  SubmitBribeRequest,
  VoteRequest,
} from './game-api.types';
import { GameApiService } from './game-api.service';

const IDENTITY_STORAGE_KEY = 'bribery.player.identity';

@Injectable({ providedIn: 'root' })
export class GameStoreService implements OnDestroy {
  private readonly gameStateSubject = new BehaviorSubject<GameState | null>(null);
  readonly gameState$ = this.gameStateSubject.asObservable();

  private readonly playerSubject = new BehaviorSubject<PlayerIdentity | null>(this.loadIdentity());
  readonly player$ = this.playerSubject.asObservable();

  private pollSubscription?: Subscription;

  constructor(private readonly api: GameApiService) {
    const identity = this.playerSubject.value;
    if (identity) {
      this.refresh(identity.gameId);
      this.startPolling(identity.gameId);
    }
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
  }

  createGame(hostName: string, settings?: GameSettings): Observable<GameState> {
    return this.api.createGame(hostName, settings).pipe(
      tap((state) => {
        const host = state.players.find((p) => p.isHost);
        if (!host) {
          throw new Error('Host player missing from game state.');
        }
        const identity: PlayerIdentity = {
          playerId: host.id,
          gameId: state.id,
          name: host.name,
          isHost: true,
          code: state.code,
        };
        this.persistIdentity(identity);
        this.playerSubject.next(identity);
        this.gameStateSubject.next(state);
        this.startPolling(state.id);
      })
    );
  }

  joinGame(code: string, name: string): Observable<JoinGameResponse> {
    return this.api.joinGame(code, name, this.playerSubject.value?.playerId).pipe(
      tap((response) => {
        const identity: PlayerIdentity = {
          playerId: response.player.id,
          gameId: response.game.id,
          name: response.player.name,
          isHost: response.player.isHost,
          code: response.game.code,
        };
        this.persistIdentity(identity);
        this.playerSubject.next(identity);
        this.gameStateSubject.next(response.game);
        this.startPolling(response.game.id);
      })
    );
  }

  startGame(): Observable<GameState> {
    const identity = this.ensureIdentity();
    return this.api.startGame(identity.gameId, identity.playerId).pipe(
      tap((state) => this.gameStateSubject.next(state))
    );
  }

  updateSettings(settings: GameSettings): Observable<GameState> {
    const identity = this.ensureIdentity();
    return this.api.updateSettings(identity.gameId, identity.playerId, settings).pipe(
      tap((state) => this.gameStateSubject.next(state))
    );
  }

  confirmPrompt(request: Omit<PromptSelectionRequest, 'playerId'> & { prompt: string; source: PromptSelectionRequest['source'] }): Observable<GameState> {
    const identity = this.ensureIdentity();
    return this.api
      .confirmPrompt(identity.gameId, {
        playerId: identity.playerId,
        prompt: request.prompt,
        source: request.source,
      })
      .pipe(tap((state) => this.gameStateSubject.next(state)));
  }

  submitBribe(request: Omit<SubmitBribeRequest, 'playerId'> & { content: string }): Observable<GameState> {
    const identity = this.ensureIdentity();
    return this.api
      .submitBribe(identity.gameId, {
        playerId: identity.playerId,
        targetId: request.targetId,
        type: request.type,
        content: request.content,
      })
      .pipe(tap((state) => this.gameStateSubject.next(state)));
  }

  castVote(chosenBriberId: string): Observable<GameState> {
    const identity = this.ensureIdentity();
    return this.api
      .castVote(identity.gameId, {
        playerId: identity.playerId,
        chosenBriberId,
      })
      .pipe(tap((state) => this.gameStateSubject.next(state)));
  }

  advance(): Observable<GameState> {
    const identity = this.ensureIdentity();
    return this.api.advance(identity.gameId, { playerId: identity.playerId }).pipe(
      tap((state) => this.gameStateSubject.next(state))
    );
  }

  updateConnection(isConnected: boolean): Observable<void> {
    const identity = this.ensureIdentity();
    const payload: ConnectionUpdateRequest = {
      playerId: identity.playerId,
      isConnected,
    };
    return this.api.updateConnection(identity.gameId, payload);
  }

  kickPlayer(playerId: string): Observable<GameState> {
    const identity = this.ensureIdentity();
    const payload: KickPlayerRequest = {
      hostId: identity.playerId,
      playerId,
    };
    return this.api.kickPlayer(identity.gameId, payload).pipe(tap((state) => this.gameStateSubject.next(state)));
  }

  clearIdentity(): void {
    localStorage.removeItem(IDENTITY_STORAGE_KEY);
    this.playerSubject.next(null);
    this.gameStateSubject.next(null);
    this.pollSubscription?.unsubscribe();
  }

  private startPolling(gameId: string): void {
    this.pollSubscription?.unsubscribe();
    this.pollSubscription = timer(0, 3000)
      .pipe(
        switchMap(() => this.api.getGame(gameId)),
        tap((state) => this.gameStateSubject.next(state))
      )
      .subscribe();
  }

  private refresh(gameId: string): void {
    this.api.getGame(gameId).subscribe({
      next: (state) => this.gameStateSubject.next(state),
      error: () => this.clearIdentity(),
    });
  }

  private persistIdentity(identity: PlayerIdentity): void {
    localStorage.setItem(IDENTITY_STORAGE_KEY, JSON.stringify(identity));
  }

  private loadIdentity(): PlayerIdentity | null {
    try {
      const raw = localStorage.getItem(IDENTITY_STORAGE_KEY);
      if (!raw) {
        return null;
      }
      return JSON.parse(raw) as PlayerIdentity;
    } catch {
      return null;
    }
  }

  private ensureIdentity(): PlayerIdentity {
    const identity = this.playerSubject.value;
    if (!identity) {
      throw new Error('Player identity is not initialised.');
    }
    return identity;
  }

  get host$(): Observable<PlayerState | undefined> {
    return this.gameState$.pipe(
      filter((state): state is GameState => state !== null),
      map((state) => state.players.find((p) => p.isHost))
    );
  }
}
