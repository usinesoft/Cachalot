import { Component } from "@angular/core";
import { ConnectionService } from "./connection.service";
import { MonitoringService } from "./monitoring.service";
import { HelpService } from "./help.service";

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

  public get detailMode():boolean{
    return this.helpService.detailMode;
  }

  public set detailMode(detail:boolean){
    this.helpService.detailMode = detail;
  }


  public get isConnected(){
    return !this.service.disconnected;
  }

  constructor(public service: MonitoringService, private connectionService:ConnectionService, private helpService:HelpService) {
    service.currentCluster.subscribe(data => {
      this.cluster = data;
    });
  }

  public adminCode:string|undefined;

  public get isAdmin():boolean {
    return this.connectionService.isAdmin;
  }

  public login():void{
    this.connectionService.connectAsAdmin(this.adminCode!)
    this.adminCode = undefined;
  }

  public logout():void{
    this.connectionService.disconnectAdmin();
    this.adminCode = undefined;
  }
}
