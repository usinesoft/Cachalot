import { Component, Input, OnInit } from "@angular/core";
import { ServerHistory, ServerInfo } from "../model/connection-data";
import {
  ApexAxisChartSeries,
  ApexChart,
  ApexXAxis,
  ApexDataLabels,
  ApexTooltip,
  ApexStroke,
  ApexYAxis
} from "ng-apexcharts";

export type ChartOptions = {
  series: ApexAxisChartSeries;
  chart: ApexChart;
  xaxis: ApexXAxis;
  yaxis: ApexYAxis;
  stroke: ApexStroke;
  tooltip: ApexTooltip;
  dataLabels: ApexDataLabels;
};

@Component({
  selector: "app-server-card",
  templateUrl: "./server-card.component.html",
  styleUrls: ["./server-card.component.scss"]
})
export class ServerCardComponent implements OnInit {

  chartOptions: ChartOptions;


  constructor() {

    this.chartOptions = {
      series: [
        {
          name: "working set",
          data: []
        },
        {
          name: "non fragmented",
          data: []
        }
      ],
      chart: {
        height: 240,
        type: "area",
        toolbar: { show: false },
        // background:'#111',
        // foreColor:'#FAFAFA'
      },
      dataLabels: {
        enabled: false
      },
      stroke: {
        curve: "smooth"
      },
      xaxis: {
        type: "numeric",

        labels: {
          show: false
        }
      },

      yaxis: {
        max: 16,
        min: 0,
        labels: {
          formatter: function(value) {
            return value.toFixed(2);
          }
        },
      },
      tooltip: {
        enabled: false,
        enabledOnSeries: []
      }

    };

  }


  @Input()
  model = new ServerInfo;


  _history: ServerHistory | undefined;

  @Input()
  set history(value: ServerHistory) {
    this._history = value;
  }


  //timerSubscription:Subscription |undefined;

  ngOnInit(): void {
    // this.timerSubscription = interval(1000).subscribe(()=>{
    this.chartOptions.series[0].data = this._history?.totalMemory!;
    this.chartOptions.series[1].data = this._history?.nonFragmentedMemory!;

    console.log(this._history?.nonFragmentedMemory!);
    //   console.log('update chart ' + this.chartOptions.series[0].data.length);
    //   window.dispatchEvent(new Event('resize'));
    // });
  }

  ngOnDestroy(): void {
    //this.timerSubscription?.unsubscribe();
  }

}
