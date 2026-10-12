import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { UrlApiService } from './services/url-api.service';
import { ShortUrl, UrlAnalytics } from './services/url-types';
import { utcDayLabel } from './chart-labels';

@Component({
  selector: 'app-root',
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule],
  templateUrl: './dashboard.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  private readonly api = inject(UrlApiService);

  readonly activeTab = signal<'overview' | 'links'>('overview');
  readonly links = signal<ShortUrl[]>([]);
  readonly selectedLink = signal<ShortUrl | null>(null);
  readonly analytics = signal<UrlAnalytics | null>(null);
  readonly busy = signal(false);
  readonly loading = signal(true);
  readonly analyticsLoading = signal(false);
  readonly errorMessage = signal('');
  readonly copiedCode = signal('');
  readonly totalClicks = computed(() => this.links().reduce((total, link) => total + link.clickCount, 0));
  readonly activeLinks = computed(() => this.links().filter(link => link.isActive).length);
  readonly chartBars = computed(() => {
    const dailyClicks = this.analytics()?.dailyClicks ?? [];
    const maximum = Math.max(1, ...dailyClicks.map(day => day.clicks));
    return dailyClicks.slice(-14).map(day => ({
      ...day,
      label: utcDayLabel(day.dateUtc),
      height: Math.max(5, Math.round((day.clicks / maximum) * 100))
    }));
  });

  readonly urlForm = new FormGroup({
    url: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(2048), Validators.pattern(/^https?:\/\/.+/i)]
    })
  });

  ngOnInit(): void {
    this.reload();
  }

  selectTab(tab: 'overview' | 'links'): void {
    this.activeTab.set(tab);
    const sectionId = tab === 'overview' ? 'overview' : 'links';
    const section = document.getElementById(sectionId);
    section?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  reload(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.api.list().subscribe({
      next: links => {
        this.links.set(links);
        this.loading.set(false);
        const current = this.selectedLink();
        if (!current) {
          if (links.length > 0) {
            this.selectLink(links[0]);
          }
          return;
        }

        // Keep the selection, but use the refreshed link and reload its analytics
        // so the chart and totals match the latest data.
        const refreshed = links.find(link => link.code === current.code);
        if (refreshed) {
          this.selectedLink.set(refreshed);
          this.loadAnalytics(refreshed.code);
        } else if (links.length > 0) {
          this.selectLink(links[0]);
        } else {
          this.selectedLink.set(null);
          this.analytics.set(null);
        }
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Could not reach the URL service. Check that the API and PostgreSQL are running.');
      }
    });
  }

  createLink(): void {
    if (this.urlForm.invalid) {
      this.urlForm.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.errorMessage.set('');
    this.api.create(this.urlForm.controls.url.value).subscribe({
      next: link => {
        this.links.update(links => [link, ...links]);
        this.urlForm.reset();
        this.busy.set(false);
        this.selectLink(link);
      },
      error: () => {
        this.busy.set(false);
        this.errorMessage.set('This URL could not be shortened. Verify the address and try again.');
      }
    });
  }

  selectLink(link: ShortUrl): void {
    this.selectedLink.set(link);
    this.loadAnalytics(link.code);
  }

  private loadAnalytics(code: string): void {
    this.analytics.set(null);
    this.analyticsLoading.set(true);
    const toUtc = new Date();
    const fromUtc = new Date(toUtc.getTime() - 30 * 24 * 60 * 60 * 1000);
    this.api.getAnalytics(code, fromUtc, toUtc).subscribe({
      next: analytics => {
        // Ignore responses for a link that is no longer selected.
        if (this.selectedLink()?.code !== code) {
          return;
        }
        this.analytics.set(analytics);
        this.analyticsLoading.set(false);
      },
      error: () => this.analyticsLoading.set(false)
    });
  }

  async copyUrl(link: ShortUrl): Promise<void> {
    try {
      await navigator.clipboard.writeText(link.shortUrl);
      this.copiedCode.set(link.code);
    } catch {
      this.errorMessage.set('Clipboard access is unavailable in this browser.');
    }
  }
}
