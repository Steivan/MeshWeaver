import { Dispatch } from "@reduxjs/toolkit";
import { map, Observable, of, OperatorFunction, Subscription } from "rxjs";
import { LayoutArea } from "../contract/LayoutArea";
import { Control } from "../contract/controls/Control";
import { isOfType } from "../contract/ofType";
import { LayoutStackControl } from "../contract/controls/LayoutStackControl";
import { setNewArea } from "./setNewArea";
import { cleanupOldArea } from "./cleanupOldArea";
import { keys } from "lodash";

export const syncNestedAreas = (dispatch: Dispatch, data$: Observable<any>): OperatorFunction<LayoutArea, any> =>
    (source: Observable<LayoutArea>) =>
        source
            .pipe(subAreas())
            .pipe(scanLayoutAreas(dispatch, data$))

export const scanLayoutAreas = (dispatch: Dispatch, data$: Observable<any>) =>
    (source: Observable<LayoutArea[]>) =>
        new Observable(subscriber => {
            const state: Record<string, Subscription> = {};

            const subscription = source.subscribe({
                next: layoutAreas => {
                    layoutAreas?.filter(layoutArea => !state[layoutArea.id])
                        .forEach(layoutArea => {
                            const area$ =
                                source.pipe(
                                    map(
                                        layoutAreas =>
                                            layoutAreas.find(area => area.id === layoutArea.id)
                                    )
                                );

                            const subscription = new Subscription();

                            subscription.add(
                                area$.pipe(syncNestedAreas(dispatch, data$)).subscribe()
                            );

                            subscription.add(
                                area$.pipe(setNewArea(dispatch, data$)).subscribe()
                            );

                            // subscription.add(
                            //     area$.pipe(cleanupOldArea(dispatch)).subscribe()
                            // );

                            state[layoutArea.id] = subscription;
                        });

                    keys(state).forEach(id => {
                        if (!layoutAreas?.find(area => area.id === id)) {
                            state[id].unsubscribe();
                            delete state[id];
                        }
                    })
                    subscriber.next(keys(state));
                },
                complete: () => subscriber.complete(),
                error: err => subscriber.error(err)
            });

            return () => subscription.unsubscribe();
        });

export const subAreas = () =>
    (source: Observable<LayoutArea>) =>
        source
            .pipe(
                map(
                    layoutArea => layoutArea?.control &&
                        getNestedAreas(layoutArea?.control)
                )
            );

const getNestedAreas = (control: Control) => {
    if (isOfType(control, LayoutStackControl)) {
        return control?.areas;
    }
}