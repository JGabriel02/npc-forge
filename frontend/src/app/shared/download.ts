import { HttpResponse } from '@angular/common/http';

/** Nome do arquivo no cabeçalho Content-Disposition (prefere o filename* codificado). */
export function fileNameFrom(contentDisposition: string | null, fallback: string): string {
  if (!contentDisposition) return fallback;

  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition);
  if (encoded) return decodeURIComponent(encoded[1].trim());

  const plain = /filename="?([^";]+)"?/i.exec(contentDisposition);
  return plain ? plain[1].trim() : fallback;
}

/** Faz o navegador baixar o arquivo da resposta, com o nome que a API mandou. */
export function saveResponse(response: HttpResponse<Blob>, fallback: string): void {
  const url = URL.createObjectURL(response.body!);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileNameFrom(response.headers.get('Content-Disposition'), fallback);
  link.click();
  // Libera a memória depois que o download começou.
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
