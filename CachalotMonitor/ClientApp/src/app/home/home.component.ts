
import { Component, OnDestroy, OnInit } from "@angular/core";
import { interval, Subscription } from "rxjs";
import { ClusterInformation, ClusterNode, ConnectionData, ServerInfo } from "../model/connection-data";
import { MatSnackBar } from "@angular/material/snack-bar";
import { MonitoringService } from "../monitoring.service";
import { ScreenStateService } from "../screen-state.service";
//import { ToastrService } from 'ngx-toastr';

@Component({
  selector: "app-home",
  templateUrl: "./home.component.html",
  styleUrls: ["./home.component.scss"]
})
export class HomeComponent implements OnInit, OnDestroy {

  connection = new ConnectionData;

  canConnect = false;

  private timerSubscription: Subscription | undefined;
  private dataSubscription: Subscription | undefined;

  get clusterHistory() {
    return this.service.clusterHistory;
  };

  clusterStatus: ServerInfo[] = [];

  constructor(private service: MonitoringService,
    private snackBar: MatSnackBar,
    private stateService: ScreenStateService) {
    const defaultNode = new ClusterNode();
    defaultNode.host = "localhost";
    defaultNode.port = 48401;
    this.connection.nodes.push(defaultNode);
  }


  get connected(): boolean {

    if (!this.service.clusterInformation.getValue()) {
      return false;
    }

    return this.service.clusterInformation.getValue()?.status != "NotConnected";
  }

  get working(): boolean {
    return this.service.working;
  }

  get disconnecting(): boolean {
    return this.service.disconnecting;
  }


  get connectionString(): string | undefined {
    return this.service.connectionString;
  }

  get clusterInformation(): ClusterInformation | null {
    return this.service.clusterInformation.getValue();
  }

  get history(): string[] {
    return this.service.history;
  }

  identifyServer(index: Number, server: ServerInfo) {
    if (!server) {
      return "";
    }
    return `${server.host}:${server.port}`;
  }


  ngOnInit() {

    this.timerSubscription = interval(400)
      .subscribe(x => {
        this.onFastTimer();

      });


    this.service.getConnectionHistory();

    this.dataSubscription = this.service.clusterInformation.subscribe(data => {

      if (!data)
        return;

      if (data?.status == "NotConnected") {

        this.clusterStatus = [];
        return;
      }

      this.service.connectionString = data?.connectionString;

      if (this.clusterStatus.length == 0) { // init once
        data?.serversStatus.forEach(element => {
          this.clusterStatus.push(element);
        });

      } else { // update

        for (let i = 0; i < this.clusterHistory.length; i++) {
          const status = data?.serversStatus[i];
          this.clusterStatus[i] = status!;
        }
      }

      // console.log("resize event");
      // console.log(this.clusterHistory[0].nonFragmentedMemory);
      window.dispatchEvent(new Event("resize"));

    });


  }


  //every 400 ms
  private onFastTimer(): void {
    this.checkCanConnect();
  }


  ngOnDestroy(): void {
    this.timerSubscription?.unsubscribe();
    this.dataSubscription?.unsubscribe();
  }


  connect() {
    this.clusterStatus = [];
    this.service.connect(this.connection);
    this.stateService.clearScreenState();

  }

  connectWithHistory(entry: string) {
    this.clusterStatus = [];
    this.service.connectWithHistory(entry);
  }

  disconnect() {

    this.service.disconnect();

  }

  addServer() {
    const node = new ClusterNode;
    node.port = 48401;
    this.connection.nodes.push(node);

    this.checkCanConnect();
  }

  removeServer(toRemove: ClusterNode) {
    const index = this.connection.nodes.indexOf(toRemove);
    this.connection.nodes.splice(index, 1);
    this.checkCanConnect();
  }


  checkCanConnect() {

    let result = true;
    this.connection.nodes.forEach(element => {
      if (!element.host || !element.port)
        result = false;
    });

    this.canConnect = result;
  }

}
