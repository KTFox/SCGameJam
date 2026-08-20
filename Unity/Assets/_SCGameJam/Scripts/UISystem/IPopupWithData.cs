namespace SCJam.UISystem
{
    public interface IPopupWithData<in TData>
    {
        void Setup(TData data);
    }
}
