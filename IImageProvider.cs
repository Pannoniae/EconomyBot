namespace EconomyBot; 

public interface IImageProvider {
    public Task<string> getRandomImage();
}