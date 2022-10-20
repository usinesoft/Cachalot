import { HttpClient } from '@angular/common/http';
import { Component, Inject, OnDestroy, OnInit } from '@angular/core';
import { interval, Subscription } from 'rxjs';
import { ClusterInformation, ClusterNode, ConnectionData, ConnectionResponse } from '../model/connection-data';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MonitoringService } from '../monitoring.service';


@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent implements OnInit, OnDestroy {

  public connection: ConnectionData = new ConnectionData;

  public canConnect: boolean = false;

  private timerSubscription: Subscription | undefined;

  constructor(private service:MonitoringService, private snackBar: MatSnackBar) {
    var defaultNode = new ClusterNode();
    defaultNode.host = 'localhost';
    defaultNode.port = 48401;
    this.connection.nodes.push(defaultNode);
  }


  public connected:boolean = false;

  public get connectionString():string|undefined{
    return this.service.connectionString;
  }

  public get clusterInformation():ClusterInformation{
    return this.service.clusterInfo;
  }

  ngOnInit() {


    // init the timer
    let tick:number = 0;
    this.timerSubscription = interval(400)
      .subscribe(x => {  
        this.onFastTimer();

        if(tick%5 == 0){
          this.onSlowTimer();
        }

        tick++;
      });

    this.service.connected.subscribe(value => {
      this.connected = value;
      
    });
  }


  //every 400 ms
  private onFastTimer():void{
    this.checkCanConnect();
  }

  // every 2 seconds
  private onSlowTimer():void{
    if(this.connected){
      this.service.updateClusterStatus();
    }
  }



  ngOnDestroy(): void {
    this.timerSubscription?.unsubscribe();
  }


  public connect() {

    this.service.connect(this.connection);

  }

  public disconnect() {
    this.connected = false;
  }

  public addServer() {
    var node = new ClusterNode;
    node.port = 48401;
    this.connection.nodes.push(node);

    this.checkCanConnect();
  }

  public removeServer(toRemove: ClusterNode) {
    var index = this.connection.nodes.indexOf(toRemove);
    this.connection.nodes.splice(index, 1);
    this.checkCanConnect();
  }


  public checkCanConnect() {

    let result = true;
    this.connection.nodes.forEach(element => {
      if (!element.host || !element.port)
        result = false;
    });

    this.canConnect = result;
  }

}
