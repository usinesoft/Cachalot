import { BrowserModule } from "@angular/platform-browser";
import { NgModule } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { HTTP_INTERCEPTORS, HttpClientModule } from "@angular/common/http";
import { RouterModule } from "@angular/router";

import { AppComponent } from "./app.component";

import { HomeComponent } from "./home/home.component";
import { BrowserAnimationsModule } from "@angular/platform-browser/animations";

import { MatFormFieldModule } from "@angular/material/form-field"
import { MatInputModule } from "@angular/material/input"
import { MatIconModule } from "@angular/material/icon"
import { MatButtonModule } from "@angular/material/button"
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { ClipboardModule } from "@angular/cdk/clipboard";
import { MatCardModule } from "@angular/material/card";
import { MatDividerModule } from "@angular/material/divider";
import { MatSnackBarModule } from "@angular/material/snack-bar";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { FormatSizePipe, EvictionPipe, IndexPipe, CheckPipe } from "./pipes";
import { CollectionCardComponent } from "./collection-card/collection-card.component";
import { TooltipModule } from "ng2-tooltip-directive";
import { CommonModule } from "@angular/common";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { MatSelectModule } from "@angular/material/select";
import { SchemaComponent } from "./schema/schema.component";
import { DataComponent } from "./data/data.component";
import { NgxMatSelectSearchModule } from "ngx-mat-select-search";
import { SimpleQueryComponent } from "./simple-query/simple-query.component";
import { AndQueryComponent } from "./and-query/and-query.component";
import { SmartMultiSelectComponent } from "./smart-multi-select/smart-multi-select.component";
import { NgxJsonViewerModule } from "ngx-json-viewer";
import { AdminComponent } from "./admin/admin.component";
import { ExecutionPlanComponent } from "./execution-plan/execution-plan.component";
import { MatDialogModule } from "@angular/material/dialog";
import { MatToolbarModule } from "@angular/material/toolbar"
import { MatSidenavModule } from "@angular/material/sidenav"
import { MatListModule } from "@angular/material/list";
import { CollectionsComponent } from "./collections/collections.component";
import { ServerCardComponent } from "./server-card/server-card.component"
import { NgApexchartsModule } from "ng-apexcharts";
import { MyInterceptor } from "./my-auth-interceptor";
import {ConnectedGuard} from "./monitoring.service"




@NgModule({
  declarations: [
    AppComponent,
    HomeComponent,
    FormatSizePipe,
    IndexPipe,
    EvictionPipe,
    CheckPipe,
    CollectionCardComponent,
    SchemaComponent,
    DataComponent,
    SimpleQueryComponent,
    AndQueryComponent,
    SmartMultiSelectComponent,
    AdminComponent,
    ExecutionPlanComponent,
    CollectionsComponent,
    ServerCardComponent
  ],
  imports: [
    BrowserModule.withServerTransition({ appId: "ng-cli-universal" }),
    HttpClientModule,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatCardModule,
    MatDividerModule,
    ClipboardModule,
    MatSnackBarModule,
    TooltipModule,
    CommonModule,
    MatProgressBarModule,
    MatSelectModule,
    NgxMatSelectSearchModule,
    NgxJsonViewerModule,
    MatCheckboxModule,
    MatSlideToggleModule,
    RouterModule.forRoot([
      { path: "", component: HomeComponent, pathMatch: "full" },
      { path: "collections", component: CollectionsComponent , canActivate:[ConnectedGuard]},
      { path: "admin", component: AdminComponent, canActivate:[ConnectedGuard] },
      { path: "schema", component: SchemaComponent , canActivate:[ConnectedGuard]},
      { path: "data", component: DataComponent , canActivate:[ConnectedGuard]},
    ]),
    BrowserAnimationsModule,
    MatDialogModule,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    NgApexchartsModule,
  ],
  providers: [{ provide: HTTP_INTERCEPTORS, useClass: MyInterceptor, multi: true }, ConnectedGuard],
  bootstrap: [AppComponent]
})
export class AppModule {
}
