import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ShortUrl, UrlAnalytics } from './url-types';

@Injectable({ providedIn: 'root' })
export class UrlApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = 'http://localhost:5080/api/urls';

  list(): Observable<ShortUrl[]> {
    return this.http.get<ShortUrl[]>(this.apiUrl);
  }

  create(url: string): Observable<ShortUrl> {
    return this.http.post<ShortUrl>(this.apiUrl, { url });
  }

  getAnalytics(code: string, fromUtc: Date, toUtc: Date): Observable<UrlAnalytics> {
    const params = new HttpParams()
      .set('fromUtc', fromUtc.toISOString())
      .set('toUtc', toUtc.toISOString());
    return this.http.get<UrlAnalytics>(`${this.apiUrl}/${encodeURIComponent(code)}/analytics`, { params });
  }
}