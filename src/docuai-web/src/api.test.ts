import { afterEach, describe, expect, it, vi } from 'vitest';
import { ask, listDocuments, upload } from './api';

afterEach(() => vi.unstubAllGlobals());

describe('DocuAI API client', () => {
  it('loads the document list from the application API', async () => {
    const documents = [{ id: 'd1', fileName: 'guide.txt', status: 'Ready' }];
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(documents), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(listDocuments()).resolves.toEqual(documents);
    expect(fetchMock).toHaveBeenCalledWith('/api/documents', undefined);
  });

  it('sends uploaded files as multipart form data', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ documentId: 'd1', status: 'Uploaded' }), { status: 202 }));
    vi.stubGlobal('fetch', fetchMock);
    const file = new File(['hello'], 'notes.txt', { type: 'text/plain' });

    await upload(file);
    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(init.method).toBe('POST');
    expect((init.body as FormData).get('file')).toBe(file);
  });

  it('preserves the conversation id and exposes safe server errors', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ detail: 'Configure Foundry.' }), { status: 503 }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(ask('What does the policy say?', 'conversation-1')).rejects.toThrow('Configure Foundry.');
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({ question: 'What does the policy say?', conversationId: 'conversation-1' });
  });
});
