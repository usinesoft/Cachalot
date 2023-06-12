import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

const FILE_SIZE_UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];
const FILE_SIZE_UNITS_LONG = ['Bytes', 'Kilobytes', 'Megabytes', 'Gigabytes', 'Pettabytes', 'Exabytes', 'Zettabytes', 'Yottabytes'];

@Pipe({
  name: 'formatSize'
})
export class FormatSizePipe implements PipeTransform {
  transform(sizeInBytes: number, longForm: boolean): string {
    const units = longForm
      ? FILE_SIZE_UNITS_LONG
      : FILE_SIZE_UNITS;

    let power = Math.round(Math.log(sizeInBytes) / Math.log(1024));
    power = Math.min(power, units.length - 1);

    const size = sizeInBytes / Math.pow(1024, power); // size in new units
    const formattedSize = Math.round(size * 100) / 100; // keep up to 2 decimals
    const unit = units[power];

    return `${formattedSize} ${unit}`;
  }
}

@Pipe({
  name: 'evictionShortName'
})
export class EvictionPipe implements PipeTransform {
  transform(longName: string|undefined): string {
    switch(longName){
      case 'LessRecentlyUsed':
        return 'LRU';

        case 'LessRecentlyUsed':
          return 'LRU';
        case 'TimeToLive':
          return 'TTL';

        default:
          return 'NO';

    }
  }
}

@Pipe({
  name: 'formatIndexType'
})
export class IndexPipe implements PipeTransform {
  
  constructor(private sanitized: DomSanitizer){}
  
  transform(longName: string|undefined) {
    switch(longName){
      case 'Primary':
        return this.sanitized.bypassSecurityTrustHtml('<div style="color:#B00020;font-weight:500;">primary</div>');

      case 'Ordered':
        return this.sanitized.bypassSecurityTrustHtml('<span style="color:#6200EE;font-weight:500;">ordered</span>');
        
      
      case 'Dictionary':
        return this.sanitized.bypassSecurityTrustHtml('<span style="color:#018786;font-weight:500;">dictionary</span>');
      
        default:
          return '';

    }
  }
}

@Pipe({
  name: 'check'
})
export class CheckPipe implements PipeTransform {
  
  constructor(private sanitized: DomSanitizer){}
  
  transform(value: boolean|undefined) {
    
    if(value){
      return this.sanitized.bypassSecurityTrustHtml('<mat-icon aria-hidden="false" aria-label="Example home icon" fontIcon="home">check</mat-icon>');
    }
    
    return '';
    
  }
}