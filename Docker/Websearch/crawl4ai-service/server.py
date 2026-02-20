"""
Crawl4AI HTTP Service
Provides web scraping and content extraction as a REST API
"""

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, HttpUrl
from crawl4ai import WebCrawler
import uvicorn

app = FastAPI(title="Crawl4AI Service", version="1.0.0")

class CrawlRequest(BaseModel):
    url: HttpUrl
    extract_text: bool = True
    extract_links: bool = False
    max_depth: int = 1

class CrawlResponse(BaseModel):
    url: str
    markdown: str
    html: str | None = None
    links: list[str] | None = None

@app.post("/crawl", response_model=CrawlResponse)
async def crawl_webpage(request: CrawlRequest):
    """
    Crawl a webpage and extract content.
    
    Args:
        request: CrawlRequest with URL and crawling parameters
        
    Returns:
        CrawlResponse with extracted content
    """
    try:
        crawler = WebCrawler()
        result = crawler.run(
            url=str(request.url),
            bypass_cache=True
        )
        
        return CrawlResponse(
            url=str(request.url),
            markdown=result.markdown or "",
            html=result.html if request.extract_text else None,
            links=result.links if request.extract_links else None
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Crawl failed: {str(e)}")

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {"status": "healthy", "service": "crawl4ai"}

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)
