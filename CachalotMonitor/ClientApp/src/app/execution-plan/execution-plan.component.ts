import { Component, Inject, OnInit } from "@angular/core";
import { MAT_DIALOG_DATA } from "@angular/material/dialog";
import { ExecutionPlan, QueryExecutionPlan } from "../model/execution-plan";
import { QueryService } from "../query.service";

@Component({
  selector: "app-execution-plan",
  templateUrl: "./execution-plan.component.html",
  styleUrls: ["./execution-plan.component.scss"]
})
export class ExecutionPlanComponent implements OnInit {

  constructor(@Inject(MAT_DIALOG_DATA) public data: DialogData, private queryService: QueryService) {}

  executionPlan: ExecutionPlan | undefined;

  // A general execution plan  has more query plans (one for each query with OR operator)
  // In this case we are only interested by one
  get queryPlan(): QueryExecutionPlan | undefined {
    const plans = this.executionPlan?.queryPlans;
    if (plans) {
      return plans[0];
    }
    return undefined;
  }

  ngOnInit(): void {
    this.queryService.GetExecutionPlan(this.data.queryId).subscribe({
      next: (data) => this.executionPlan = data
    });
  }

}

export interface DialogData {
  queryId: string;
  clientTimeInMilliseconds: number;
}
