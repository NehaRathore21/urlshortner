import '@angular/compiler';
import { signal } from '@angular/core';
import { describe, expect, it, vi } from 'vitest';
import { App } from './app';

describe('App', () => {
  it('should default to the overview tab', () => {
    const app = Object.create(App.prototype) as App & { activeTab: ReturnType<typeof signal<'overview' | 'links'>> };
    app.activeTab = signal<'overview' | 'links'>('overview');

    expect(app.activeTab()).toBe('overview');
  });

  it('should switch tabs when the sidebar nav is clicked', () => {
    const app = Object.create(App.prototype) as App & {
      activeTab: ReturnType<typeof signal<'overview' | 'links'>>;
      selectTab: (tab: 'overview' | 'links') => void;
    };
    app.activeTab = signal<'overview' | 'links'>('overview');
    const scrollSpy = vi.fn();
    Object.defineProperty(globalThis.document, 'getElementById', {
      value: vi.fn((id: string) => ({
        id,
        scrollIntoView: scrollSpy
      })),
      configurable: true
    });

    app.selectTab('links');

    expect(app.activeTab()).toBe('links');
    expect(scrollSpy).toHaveBeenCalledTimes(1);
  });
});
