export const dragStepMinutes = 5;

export interface ScheduleBlock {
  start: number;
  duration: number;
}

export function previewScheduleDrop(
  blocks: ScheduleBlock[], activity: ScheduleBlock, offset: number, firstMinute: number, lastMinute: number
) {
  const steps = Math.sign(offset) * Math.round(Math.abs(offset) / dragStepMinutes);
  const firstStep = Math.ceil((firstMinute - activity.start) / dragStepMinutes);
  const lastStep = Math.floor((lastMinute - activity.duration - activity.start) / dragStepMinutes);
  const start = activity.start + Math.max(firstStep, Math.min(lastStep, steps)) * dragStepMinutes;
  const end = start + activity.duration;
  return { start, blocked: blocks.some(block => block.start < end && start < block.start + block.duration) };
}
