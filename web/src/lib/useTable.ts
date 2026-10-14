import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';
import type { SortingState } from '@tanstack/react-table';
import { get, qs, type Page } from './api';

const PAGE_SIZE = 25;

/**
 * Table state lives in the URL (filters, sort, page), so chart drill-downs are plain links
 * and every view can be bookmarked. Refetches keep the previous rows on screen (no flash).
 */
export function useTable<T>(endpoint: string, filterKeys: string[], fixed: Record<string, string> = {}) {
  const [params, setParams] = useSearchParams();
  const filters = Object.fromEntries(filterKeys.map((k) => [k, params.get(k) ?? ''])) as Record<string, string>;
  const sort = params.get('sort');
  const desc = params.get('desc') === 'true';
  const page = Number(params.get('page') ?? 1) || 1;
  const query = { ...filters, ...fixed, sort, desc: sort ? desc : undefined, page, size: PAGE_SIZE };

  const result = useQuery({
    queryKey: [endpoint, query],
    queryFn: () => get<Page<T>>(`/api/tables/${endpoint}${qs(query)}`),
    placeholderData: keepPreviousData,
  });

  const update = (patch: Record<string, string | null>, resetPage = true) => {
    const next = new URLSearchParams(params);
    for (const [k, v] of Object.entries(patch)) (v === null || v === '' ? next.delete(k) : next.set(k, v));
    if (resetPage) next.delete('page');
    setParams(next, { replace: true });
  };

  const sorting: SortingState = sort ? [{ id: sort, desc }] : [];
  return {
    data: result.data,
    loading: result.isFetching,
    error: result.error,
    filters,
    setFilter: (key: string, value: string) => update({ [key]: value }),
    table: {
      rows: result.data?.rows ?? [],
      total: result.data?.total ?? 0,
      page,
      size: PAGE_SIZE,
      sorting,
      loading: result.isFetching,
      onSortingChange: (s: SortingState) => update({ sort: s[0]?.id ?? null, desc: s[0]?.desc ? 'true' : null }),
      onPageChange: (p: number) => update({ page: String(p) }, false),
    },
    /** Export link with the same filters and sort as the table on screen. */
    exportHref: `/api/tables/${endpoint}/export${qs({ ...filters, ...fixed, sort, desc: sort ? desc : undefined })}`,
  };
}
