export type GamePhase =
  | 'Lobby'
  | 'PromptSelection'
  | 'Submission'
  | 'Voting'
  | 'Scoreboard'
  | 'Finished';

export interface GameSettings {
  totalRounds: number;
  promptSelectionTimerSeconds: number;
  submissionTimerSeconds: number;
  votingTimerSeconds: number;
  resultsTimerSeconds: number;
  customPromptsEnabled: boolean;
}

export interface PlayerState {
  id: string;
  name: string;
  isHost: boolean;
  isConnected: boolean;
  score: number;
  isWaiting: boolean;
}

export interface BribeRecordEntry {
  targetId: string;
  content: BribeContent;
  isRandom: boolean;
}

export interface BribeContent {
  type: 'Text' | 'Image';
  content: string;
}

export interface BribeForTarget {
  submittedBy: string;
  targetId: string;
  content: BribeContent;
  isRandom: boolean;
}

export interface PromptSelectionState {
  text: string;
  source: 'Library' | 'Custom' | 'Random';
}

export interface RoundSnapshot {
  roundNumber: number;
  assignments: Record<string, string[]>;
  submissions: Record<string, BribeRecordEntry[]>;
  bribesByTarget: Record<string, BribeForTarget[]>;
  pendingPromptConfirmations: string[];
  pendingSubmissions: string[];
  pendingVotes: string[];
  promptsByTarget: Record<string, PromptSelectionState>;
}

export interface PlayerScoreDelta {
  playerId: string;
  roundPoints: number;
  totalScore: number;
}

export interface PromptResult {
  targetPlayerId: string;
  prompt: string;
  winningPlayerId: string;
  wasRandom: boolean;
}

export interface RoundSummary {
  roundNumber: number;
  scoreboard: PlayerScoreDelta[];
  promptResults: PromptResult[];
}

export interface GameState {
  id: string;
  code: string;
  phase: GamePhase;
  settings: GameSettings;
  currentRound: number;
  players: PlayerState[];
  round?: RoundSnapshot | null;
  completedRounds: RoundSummary[];
  phaseEndsAt?: string | null;
}

export interface PlayerIdentity {
  playerId: string;
  gameId: string;
  name: string;
  isHost: boolean;
  code: string;
}
