import type { Task, TaskStatus } from '../types/task';

// Interfaces the UI depends on. Real (AWS) and mock implementations both satisfy these, so the
// app can run fully local (mock) or against a deployed backend (live) with no component changes.

export interface AuthGateway {
  /** The signed-in user's login id, or null when there is no session. */
  currentEmail(): Promise<string | null>;
  signIn(email: string, password: string): Promise<void>;
  signOut(): Promise<void>;
  /** The bearer token sent to the API. */
  getIdToken(): Promise<string>;
}

export interface TaskGateway {
  list(): Promise<Task[]>;
  create(title: string): Promise<Task>;
  update(id: string, fields: { title?: string; status?: TaskStatus }): Promise<Task>;
  remove(id: string): Promise<void>;
}
