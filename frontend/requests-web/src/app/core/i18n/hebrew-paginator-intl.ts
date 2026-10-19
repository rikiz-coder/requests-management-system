import { Injectable } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';

@Injectable()
export class HebrewPaginatorIntl extends MatPaginatorIntl {
  override itemsPerPageLabel = 'שורות בעמוד:';
  override nextPageLabel = 'העמוד הבא';
  override previousPageLabel = 'העמוד הקודם';
  override firstPageLabel = 'העמוד הראשון';
  override lastPageLabel = 'העמוד האחרון';

  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (length === 0) {
      return 'אין תוצאות';
    }
    const start = page * pageSize + 1;
    const end = Math.min(length, (page + 1) * pageSize);
    return `${start.toLocaleString('he-IL')}–${end.toLocaleString('he-IL')} מתוך ${length.toLocaleString('he-IL')}`;
  };
}
