import { Component } from "@angular/core";
import { MonitoringService } from "./monitoring.service";

@Component({
  selector: "app-root",
  templateUrl: "./app.component.html"
})
export class AppComponent {
  title = "app";
  isExpanded = true;
  showSubmenu = false;
  isShowing = false;
  showSubSubMenu = false;

  mouseenter() {
    if (!this.isExpanded) {
      this.isShowing = true;
    }
  }

  mouseleave() {
    if (!this.isExpanded) {
      this.isShowing = false;
    }
  }

  cluster: string | null = null;

  constructor(public service: MonitoringService) {
    service.currentCluster.subscribe(data => {
      this.cluster = data;
    });
  }
}
