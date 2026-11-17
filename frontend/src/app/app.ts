import { Component, inject, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { CurrencyPipe, DatePipe } from '@angular/common';

interface Product { id: number; name: string; price: number; createdAt: string; }

@Component({
  selector: 'app-root',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  private http = inject(HttpClient);
  private api = '/api/products';

  products = signal<Product[]>([]);
  error = signal('');
  loading = signal(false);
  editingId = signal<number | null>(null);
  form = { name: '', price: 0 };

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.http.get<Product[]>(this.api).subscribe({
      next: p => { this.products.set(p); this.loading.set(false); },
      error: e => this.fail(e)
    });
  }

  save() {
    const body = { name: this.form.name, price: Number(this.form.price) };
    const id = this.editingId();
    const req = id === null ? this.http.post<Product>(this.api, body) : this.http.put<Product>(`${this.api}/${id}`, body);
    req.subscribe({ next: () => { this.cancel(); this.load(); }, error: e => this.fail(e) });
  }

  edit(p: Product) { this.editingId.set(p.id); this.form = { name: p.name, price: p.price }; }

  cancel() { this.editingId.set(null); this.form = { name: '', price: 0 }; this.error.set(''); }

  remove(p: Product) {
    this.http.delete(`${this.api}/${p.id}`).subscribe({ next: () => this.load(), error: e => this.fail(e) });
  }

  private fail(e: any) { this.loading.set(false); this.error.set(e?.error?.error ?? e?.message ?? 'Request failed'); }
}
