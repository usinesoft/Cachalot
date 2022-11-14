import { HttpClient } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AndQuery, DataResponse, QueryMetadata, SqlResponse } from './model/query';

@Injectable({
  providedIn: 'root'
})
export class QueryService {

  constructor(private http: HttpClient, @Inject('BASE_URL') private baseUrl: string) { }


  public  GetQueryMetadata(collection:string, property:string):Observable<QueryMetadata>{
    return this.http.get<QueryMetadata>(this.baseUrl + `Data/query/metadata/${collection}/${property}`);
  }


  public  GetAsSql(collection:string, query:AndQuery):Observable<SqlResponse>{
    return this.http.post<SqlResponse>(this.baseUrl + `Data/query/sql/${collection}`, query);
  }

  public ExecuteQuery(sql:string){

    var request = new SqlResponse();
    request.sql = sql;
    return this.http.post<DataResponse>(this.baseUrl + 'Data/query/execute', request);
  }

}
