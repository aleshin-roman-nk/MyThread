import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

export interface PlannedActivityReference { projectId: number; taskId: number | null; name: string; projectName?: string | null; }
export interface PlannedActivity {
  id: number; date: string; startTime: string; durationMinutes: number; end: string; description: string;
  subject: { id: number; name: string; shortName: string } | null; references: PlannedActivityReference[];
}
export interface MovePlannedActivity { originalStart: string; durationMinutes: number; start: string; }
export interface SavePlannedActivity {
  date: string; startTime: string; durationMinutes: number; description: string; subjectId: number | null;
  projectIds: number[]; taskIds: number[];
}

const plannedActivitiesUrl = '/api/planning/activities';

@Injectable({ providedIn: 'root' })
export class PlanningApi {
  private readonly http = inject(HttpClient);

  plannedActivities(from: string, through: string) {
    return this.http.get<PlannedActivity[]>(plannedActivitiesUrl, { params: { from, through } });
  }

  movePlannedActivity(id: number, position: MovePlannedActivity) {
    return this.http.put<PlannedActivity>(plannedActivitiesUrl + '/' + id + '/position', position);
  }

  savePlannedActivity(value: SavePlannedActivity, id?: number) {
    return id === undefined ? this.http.post<PlannedActivity>(plannedActivitiesUrl, value)
      : this.http.put<PlannedActivity>(plannedActivitiesUrl + '/' + id, value);
  }

  deletePlannedActivity(id: number) {
    return this.http.delete<void>(plannedActivitiesUrl + '/' + id);
  }
}
