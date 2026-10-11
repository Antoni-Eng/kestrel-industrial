import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { getKestrelConfig } from './config';
import { Equipment, FeedbackRequest, SourceDocument } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = getKestrelConfig().apiBaseUrl;

  getEquipment(): Observable<Equipment[]> {
    return this.http.get<Equipment[]>(`${this.apiBaseUrl}/api/equipment`);
  }

  getDocument(documentId: string): Observable<SourceDocument> {
    return this.http.get<SourceDocument>(
      `${this.apiBaseUrl}/api/documents/${encodeURIComponent(documentId)}`,
    );
  }

  submitFeedback(feedback: FeedbackRequest): Observable<void> {
    return this.http.post<void>(`${this.apiBaseUrl}/api/feedback`, feedback);
  }
}
