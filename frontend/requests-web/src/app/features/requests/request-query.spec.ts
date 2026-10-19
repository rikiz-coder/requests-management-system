import { convertToParamMap } from '@angular/router';

import { parseQuery, toQueryParams } from './request-query';

describe('request query ↔ URL', () => {
  it('ignores values the API would reject', () => {
    const query = parseQuery(convertToParamMap({
      status: ['Waiting', 'Archived'], priority: 'Urgent', page: '-1', pageSize: '500', sortBy: 'password',
      createdFrom: 'yesterday', createdTo: '2026-01-31',
    }));

    expect(query).toMatchObject({
      status: ['Waiting'], priority: undefined, page: 1, pageSize: 20, sortBy: 'createdAt', sortDirection: 'desc',
      createdFrom: undefined, createdTo: '2026-01-31',
    });
  });

  it('removes defaults and empty values from the URL', () => {
    expect(toQueryParams({ page: 1, sortBy: 'createdAt', search: '', status: [], assignedTo: 'דוד', page2: 3 } as never))
      .toEqual({ page: null, sortBy: null, search: null, status: null, assignedTo: 'דוד', page2: 3 });
  });
});
