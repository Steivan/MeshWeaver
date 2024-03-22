import { expect, test, describe, jest } from "@jest/globals";
import { Observable, of } from "rxjs";
import { withTeardownBefore } from "./withTeardownBefore";

describe("basic", () => {
    test("1", () => {
        const subscription =
            new Observable(
                subscriber => {
                    subscriber.next(1);
                    // subscriber.complete();
                })
                .pipe(withTeardownBefore(() => console.log("added teardown")))
                .subscribe(console.log);

        subscription.unsubscribe();
    });
});
