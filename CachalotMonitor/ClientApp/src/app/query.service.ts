import { HttpClient } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AndQuery, DataResponse, QueryMetadata, SearchRequest, SqlResponse } from './model/query';

import streamSaver from 'streamsaver';


import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class QueryService {

  constructor(private http: HttpClient, @Inject('BASE_URL') private baseUrl: string) { }


  public GetQueryMetadata(collection: string, property: string): Observable<QueryMetadata> {
    return this.http.get<QueryMetadata>(this.baseUrl + `Data/query/metadata/${collection}/${property}`);
  }


  public GetAsSql(collection: string, query: AndQuery): Observable<SqlResponse> {
    return this.http.post<SqlResponse>(this.baseUrl + `Data/query/sql/${collection}`, query);
  }

  public DownloadAsStream(sql: string | undefined, fullTextQuery: string | undefined):Observable<boolean>{
    var request = new SearchRequest();
    request.sql = sql;
    request.fullText = fullTextQuery;


    return this.downloadFileAsStream(this.baseUrl + 'data/query/stream', 'POST', request);
  }

  public ExecuteQuery(sql: string | undefined, fullTextQuery: string | undefined) {

    var request = new SearchRequest();
    request.sql = sql;
    request.fullText = fullTextQuery;
    return this.http.post<DataResponse>(this.baseUrl + 'Data/query/execute', request);
  }

  public UploadFile(file: File, collection: string): Observable<any> {

    const formData = new FormData();
    formData.append("file", file);


    return this.http.post<any>(this.baseUrl + `Data/put/stream/${collection}`, formData);
  }

  // extract file name from header
  protected getFileName(data: any) {

    return 'data.json';
    // let fileName: string|undefined;
    // let fileNameKeyword = "filename=";

    // console.log('HEADER');
    // console.log(data.headers);

    // let contentDisposition = data.headers.get('content-disposition').split("; ");
    // contentDisposition.forEach((t: string) => {
    //   if (t.includes(fileNameKeyword)) {
    //     fileName = t.replace(fileNameKeyword, '').split('"').join('');
    //   }
    // });
    // return fileName;
  }

  // download stream without a Blob (which is limited in size and accumulates everything before being saved to a file)
  public downloadFileAsStream(url: string, method: string, body: any): Observable<boolean> {
    const accessToken = localStorage.getItem('access_token');
    let headers: any = undefined;
    if (accessToken) {
      headers = {
        Authorization: `Bearer ${accessToken}`
      }
    }
    if (body) {
      headers = headers || {};
      headers = { ...headers, 'Content-Type': 'application/json' };
    }
    return new Observable<boolean>(subscriber => {
      fetch(url, {
        method: method,
        body: !!body ? JSON.stringify(body) : undefined,
        headers: headers
      })
        .then(response => {
          let fileName = this.getFileName(response);
          console.info("Creating file stream...")
          streamSaver.mitm = `assets/mitm.html`;
          const fileStream = streamSaver.createWriteStream(fileName!, { size: -1 });
          console.info("Created file stream with name " + fileName);
          response!.body!.pipeTo(fileStream)
            .then(
              _ => {

                subscriber.next(true);
                subscriber.complete();
              },
              error => {
                console.error('Error in the middle of streaming: ', error);

                subscriber.error(error);
              }
            )
            .catch(error => {
              console.error('FATAL Error in the middle of streaming: ', error);

              subscriber.error(error);
            });
        })
        .catch((error) => {
          console.error('Error: ', error);
          subscriber.error(error);
        })
    });
  }
}
