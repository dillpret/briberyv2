import { TestBed } from '@angular/core/testing';
import { BehaviorSubject, of } from 'rxjs';
import { AppComponent } from './app.component';
import { GameStoreService } from './services/game-store.service';
import { GameApiService } from './services/game-api.service';
import { GameState, PlayerIdentity } from './models/game-models';

class MockStore {
  gameState$ = new BehaviorSubject<GameState | null>(null);
  player$ = new BehaviorSubject<PlayerIdentity | null>(null);

  updateConnection = jasmine.createSpy('updateConnection').and.returnValue(of(void 0));
  startGame = jasmine.createSpy('startGame').and.returnValue(of(null));
  updateSettings = jasmine.createSpy('updateSettings').and.returnValue(of(null));
  kickPlayer = jasmine.createSpy('kickPlayer').and.returnValue(of(null));
  confirmPrompt = jasmine.createSpy('confirmPrompt').and.returnValue(of(null));
  submitBribe = jasmine.createSpy('submitBribe').and.returnValue(of(null));
  castVote = jasmine.createSpy('castVote').and.returnValue(of(null));
  advance = jasmine.createSpy('advance').and.returnValue(of(null));
}

class MockApi {
  listPrompts = jasmine.createSpy('listPrompts').and.returnValue(of(['Prompt 1', 'Prompt 2']));
}

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        { provide: GameStoreService, useClass: MockStore },
        { provide: GameApiService, useClass: MockApi },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('renders landing view when no game is active', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-landing')).not.toBeNull();
  });
});
