import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { GameSettings, GameState } from '../models/game-models';
import {
  AdvanceRequest,
  ConnectionUpdateRequest,
  CreateGameRequest,
  JoinGameResponse,
  KickPlayerRequest,
  PromptSelectionRequest,
  SubmitBribeRequest,
  VoteRequest,
} from './game-api.types';

@Injectable({ providedIn: 'root' })
export class GameApiService {
  private readonly baseUrl = environment.apiBaseUrl;

  constructor(private readonly http: HttpClient) {}

  createGame(hostName: string, settings?: GameSettings): Observable<GameState> {
    const payload: CreateGameRequest = { hostName, settings };
    return this.http.post<GameState>(`${this.baseUrl}/games`, payload);
  }

  getGame(gameId: string): Observable<GameState> {
    return this.http.get<GameState>(`${this.baseUrl}/games/${gameId}`);
  }

  listPrompts(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/games/prompts`);
  }

  joinGame(code: string, name: string, playerId?: string): Observable<JoinGameResponse> {
    return this.http.post<JoinGameResponse>(`${this.baseUrl}/games/${code}/join`, {
      name,
      playerId,
    });
  }

  startGame(gameId: string, playerId: string): Observable<GameState> {
    return this.http.post<GameState>(`${this.baseUrl}/games/${gameId}/start`, { playerId });
  }

  updateSettings(gameId: string, playerId: string, settings: GameSettings): Observable<GameState> {
    return this.http.post<GameState>(`${this.baseUrl}/games/${gameId}/settings`, {
      playerId,
      settings,
    });
  }

  confirmPrompt(gameId: string, request: PromptSelectionRequest): Observable<GameState> {
    return this.http.post<GameState>(`${this.baseUrl}/games/${gameId}/prompts`, request);
  }

  submitBribe(gameId: string, request: SubmitBribeRequest): Observable<GameState> {
    return this.http.post<GameState>(`${this.baseUrl}/games/${gameId}/submissions`, request);
  }

  castVote(gameId: string, request: VoteRequest): Observable<GameState> {
    return this.http.post<GameState>(`${this.baseUrl}/games/${gameId}/votes`, request);
  }

  advance(gameId: string, request: AdvanceRequest): Observable<GameState> {
    return this.http.post<GameState>(`${this.baseUrl}/games/${gameId}/advance`, request);
  }

  updateConnection(gameId: string, request: ConnectionUpdateRequest) {
    return this.http.post(`${this.baseUrl}/games/${gameId}/connection`, request).pipe(map(() => void 0));
  }

  kickPlayer(gameId: string, request: KickPlayerRequest): Observable<GameState> {
    return this.http.post<GameState>(`${this.baseUrl}/games/${gameId}/kick`, request);
  }
}
