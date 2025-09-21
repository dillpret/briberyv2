import { GameSettings, GameState, PlayerState } from '../models/game-models';

export interface CreateGameRequest {
  hostName: string;
  settings?: GameSettings;
}

export interface JoinGameResponse {
  player: PlayerState;
  game: GameState;
}

export interface PromptSelectionRequest {
  playerId: string;
  prompt: string;
  source: 'Library' | 'Custom' | 'Random';
}

export interface SubmitBribeRequest {
  playerId: string;
  targetId: string;
  type: 'text' | 'image';
  content: string;
}

export interface VoteRequest {
  playerId: string;
  chosenBriberId: string;
}

export interface AdvanceRequest {
  playerId: string;
}

export interface ConnectionUpdateRequest {
  playerId: string;
  isConnected: boolean;
}

export interface KickPlayerRequest {
  hostId: string;
  playerId: string;
}
