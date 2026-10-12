export interface ShortUrl {
  id: number;
  code: string;
  originalUrl: string;
  shortUrl: string;
  createdAtUtc: string;
  expiresAtUtc: string | null;
  isActive: boolean;
  clickCount: number;
}

export interface UrlAnalytics {
  code: string;
  totalClicks: number;
  dailyClicks: Array<{ dateUtc: string; clicks: number }>;
}