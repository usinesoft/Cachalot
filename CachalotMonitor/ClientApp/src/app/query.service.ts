import { HttpClient } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { QueryMetadata } from './model/query';

@Injectable({
  providedIn: 'root'
})
export class QueryService {

  constructor(private http: HttpClient, @Inject('BASE_URL') private baseUrl: string) { }


  public  GetQueryMetadata(collection:string, property:string):Observable<QueryMetadata>{
    return this.http.get<QueryMetadata>(this.baseUrl + `Data/query/metadata/${collection}/${property}`);
  }

}
