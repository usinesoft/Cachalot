import { Component, OnDestroy, OnInit } from '@angular/core';
import { interval, Subscription } from 'rxjs';
import { ClusterNode, ConnectionData } from '../model/connection-data';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent implements OnInit, OnDestroy {

  public connection: ConnectionData = new ConnectionData;

  public canConnect:boolean = false;

  private timerSubscription:Subscription|undefined;

  public connected:boolean = false;

  public connectionString:string = "a very long connection string";

  constructor(){
    var defaultNode = new ClusterNode();
    defaultNode.host = 'localhost';
    defaultNode.port = 48401;
    this.connection.nodes.push(defaultNode);
  }
  

  ngOnInit() {
    
    this.timerSubscription = interval(400)
           .subscribe(x => { this.checkCanConnect(); });
  }


  ngOnDestroy(): void {
    this.timerSubscription?.unsubscribe();
  }


  public connect(){
    this.connected = true;
  }

  public disconnect(){
    this.connected = false;
  }

  public addServer(){
    var node = new ClusterNode;
    node.port = 48401;
    this.connection.nodes.push(node);

    this.checkCanConnect();
  }

  public removeServer(toRemove:ClusterNode) {
    var index = this.connection.nodes.indexOf(toRemove);
    this.connection.nodes.splice(index, 1);
    this.checkCanConnect();
  }


  public checkCanConnect(){

    let result = true;
    this.connection.nodes.forEach(element => {
      if(!element.host || !element.port)
        result = false;
    });

    this.canConnect = result;
  }
  
}
