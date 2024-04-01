import { configureStore, createAction, createReducer } from "@reduxjs/toolkit"
import { dataSyncHub } from "./dataSyncHub";
import { Style } from "../contract/controls/Style";
import { workspaceReducer } from "@open-smc/data/src/workspace";
import { subscribeToDataChanges } from "@open-smc/data/src/subscribeToDataChanges";
import { EntireWorkspace, LayoutAreaReference } from "@open-smc/data/src/data.contract";
import { MessageHub } from "@open-smc/message-hub/src/api/MessageHub";
import { distinctUntilKeyChanged, from, Subscription, tap } from "rxjs";
import { syncLayoutArea } from "./syncLayoutArea";
import { withPreviousValue } from "./withPreviousValue";
import { deserialize } from "../contract/deserialize";
import '../contract';

export type RootState = {
    rootArea: string;
    areas: Record<string, LayoutAreaModel>;
}

export type LayoutAreaModel = {
    id: string;
    control?: ControlModel;
    options?: any;
    style?: Style;
}

export type ControlModel = {
    componentTypeName: string;
    props: { [prop: string]: unknown };
}

export interface SetPropAction {
    areaId: string;
    prop: string;
    value: any;
}

export const setProp = createAction<SetPropAction>('setProp');
export const setArea = createAction<LayoutAreaModel>('setArea');
export const removeArea = createAction<string>('removeArea');
export const setRoot = createAction<string>('setRoot');

const rootReducer = createReducer<RootState>(
    null,
    builder => {
        builder
            .addCase(setProp, (state, action) => {
                const {areaId, prop, value} = action.payload;
                (state.areas[areaId].control.props as any)[prop] = value;
            })
            .addCase(setArea, (state, action) => {
                const area = action.payload;
                state.areas[area.id] = area;
            })
            .addCase(removeArea, (state, action) => {
                delete state.areas[action.payload];
            })
            .addCase(setRoot, (state, action) => {
                state.rootArea = action.payload;
            })
    }
);

export const makeStore = (backendHub: MessageHub) => {
    const subscription = new Subscription();

    const dataStore =
        configureStore({
            reducer: workspaceReducer,
            devTools: {
                name: "data"
            }
        });

    subscription.add(subscribeToDataChanges(backendHub, new EntireWorkspace(), dataStore.dispatch));

    const layoutStore =
        configureStore({
            reducer: workspaceReducer,
            devTools: {
                name: "layout"
            }
        });

    subscription.add(subscribeToDataChanges(backendHub, new LayoutAreaReference("/"), layoutStore.dispatch));

    const uiStore = configureStore<RootState>({
        preloadedState: {
            rootArea: null,
            areas: {}
        },
        reducer: rootReducer,
        devTools: {
            name: "ui"
        }
    });

    const rootLayoutArea$ =
        from(layoutStore)
            .pipe(deserialize());

    const data$ = from(dataStore);

    const ui$ = from(uiStore);

    const {dispatch} = uiStore;

    rootLayoutArea$
        .pipe(syncLayoutArea(data$, dispatch, ui$, dataStore.dispatch))
        .pipe(distinctUntilKeyChanged("id"))
        .pipe(withPreviousValue())
        .subscribe(([previous, current]) => {
            if (previous?.id) {
                dispatch(removeArea(previous.id));
            }
            dispatch(setRoot(current?.id ? current.id : null));
        });

    return uiStore;
}

export const store = makeStore(dataSyncHub);

export type AppStore = typeof store;

export type AppDispatch = AppStore["dispatch"];

export const layoutAreaSelector = (id: string) => (state: RootState) => state.areas[id];